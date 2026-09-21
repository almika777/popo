"use client";

import { useEffect, useLayoutEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import dayjs, { Dayjs } from "dayjs";
import {
  Alert,
  App as AntdApp,
  Button,
  Card,
  Collapse,
  DatePicker,
  Form,
  Input,
  InputNumber,
  Layout,
  Select,
  Space,
  Table,
  Typography,
} from "antd";
import type { ColumnsType } from "antd/es/table";
import {
  portfolioReturnApi,
  type CashFlowType,
  type PortfolioCashFlow,
  type PortfolioReturn,
  type PortfolioValuation
} from "../../lib/portfolio-return-api";
import type { CashBalance, PortfolioSummary } from "../../lib/portfolio-ledger-api";
import {
  Bar,
  BarChart,
  Brush,
  CartesianGrid,
  Cell,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
  type TooltipContentProps
} from "recharts";
import {
  buildCashFlowAdjustedValuations,
  buildRoundedScale,
  buildYearEndForecast
} from "../../lib/portfolio-chart";

const { Content } = Layout;

type ValuationForm = { date: Dayjs; totalValue: number; comment?: string };
type CashFlowForm = { date: Dayjs; type: CashFlowType; amount: number; comment?: string };

const money = new Intl.NumberFormat("ru-RU", { style: "currency", currency: "RUB", maximumFractionDigits: 2 });
const number = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const percent = new Intl.NumberFormat("ru-RU", { style: "percent", minimumFractionDigits: 2, maximumFractionDigits: 2 });
const formatChartAxis = (value: number) => {
  const sign = value < 0 ? "−" : "";
  const absolute = Math.abs(value);
  return absolute >= 1_000_000
    ? `${sign}${number.format(absolute / 1_000_000)} млн`
    : absolute >= 1_000
      ? `${sign}${number.format(absolute / 1_000)} тыс.`
      : `${sign}${number.format(absolute)}`;
};

function ValuationChart({
  rows,
  selectedRange,
  onRangeChange
}: {
  rows: PortfolioValuation[];
  selectedRange: [number, number] | null;
  onRangeChange: (range: [number, number] | null) => void;
}) {
  if (rows.length === 0) {
    return <div className="portfolio-chart-empty">Добавьте первую оценку, чтобы увидеть изменение портфеля.</div>;
  }

  if (rows.length === 1) {
    return (
      <div className="portfolio-chart-empty">
        <strong>{money.format(rows[0].totalValue)}</strong>
        <span>Недостаточно данных для графика изменения.</span>
      </div>
    );
  }

  const visibleRows = selectedRange
    ? rows.slice(selectedRange[0], selectedRange[1] + 1)
    : rows;
  const chartRows = visibleRows;
  const startValue = chartRows[0].totalValue;
  const lastValue = chartRows.at(-1)!.totalValue;
  const forecastChange = !selectedRange && chartRows.length > 1
    ? buildYearEndForecast(chartRows[0].date, chartRows.at(-1)!.date, lastValue - startValue)
    : undefined;
  const forecast = forecastChange
    ? { ...forecastChange, totalValue: lastValue + forecastChange.totalValue }
    : undefined;
  const scale = buildRoundedScale([
    ...chartRows.map((row) => row.totalValue),
    ...(forecast ? [forecast.totalValue] : [])
  ]);
  const chartData = [
    ...chartRows.map((row) => ({
      id: row.id,
      date: row.date,
      label: dayjs(row.date).format("MM.YYYY"),
      totalValue: row.totalValue,
      isForecast: false
    })),
    ...(forecast ? [{
      id: forecast.id,
      date: forecast.date,
      label: `12.${dayjs(forecast.date).format("YYYY")}`,
      totalValue: forecast.totalValue,
      isForecast: true
    }] : [])
  ];
  const renderTooltip = ({ active, payload }: TooltipContentProps) => {
    if (!active || !payload?.length) {
      return null;
    }

    const point = payload[0]?.payload as (typeof chartData)[number] | undefined;
    if (!point) {
      return null;
    }

    return (
      <div className={`portfolio-chart-tooltip-card${point.isForecast ? " portfolio-chart-tooltip-card-forecast" : ""}`}>
        <strong>Дата: {dayjs(point.date).format("DD.MM.YYYY")}</strong>
        <span>{money.format(point.totalValue)}</span>
      </div>
    );
  };

  return (
    <div className="portfolio-chart-wrap" role="img" aria-label="Стоимость портфеля без учета пополнений и выводов">
      <div className="portfolio-chart-recharts">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={chartData} margin={{ top: 18, right: 18, bottom: 10, left: 8 }} barCategoryGap="8%" barGap={0}>
            <CartesianGrid className="portfolio-chart-grid" vertical={false} />
            <XAxis
              dataKey="date"
              tickFormatter={(value: string) => dayjs(value).format("MM.YYYY")}
              tick={{ fill: "rgba(255, 255, 255, 0.55)", fontSize: 11 }}
              tickLine={false}
              axisLine={false}
              minTickGap={24}
              tickCount={6}
            />
            <YAxis
              type="number"
              domain={[scale.min, scale.max]}
              ticks={scale.ticks}
              tickFormatter={formatChartAxis}
              width="auto"
              tickMargin={12}
              tick={{ fill: "rgba(255, 255, 255, 0.55)", fontSize: 11 }}
              tickLine={false}
              axisLine={false}
            />
            <Tooltip
              content={renderTooltip}
              cursor={{ stroke: "rgba(255, 255, 255, 0.22)", strokeDasharray: "4 4" }}
              allowEscapeViewBox={{ x: false, y: true }}
              wrapperStyle={{ outline: "none" }}
            />
            <Bar
              dataKey="totalValue"
              radius={[3, 3, 0, 0]}
              isAnimationActive={false}
            >
              {chartData.map((point) => <Cell key={point.id} fill={point.isForecast ? "#b37feb" : "#1677ff"} />)}
            </Bar>
            {chartRows.length > 2 && <Brush
              dataKey="label"
              height={28}
              travellerWidth={10}
              stroke="#1677ff"
              fill="#141414"
              startIndex={0}
              endIndex={chartData.length - 1}
              tickFormatter={(value) => String(value)}
              onDragEnd={({ startIndex = 0, endIndex = chartData.length - 1 }) => {
                const maxActualIndex = chartRows.length - 1;
                const start = Math.min(startIndex, maxActualIndex);
                const end = Math.min(endIndex, maxActualIndex);
                const offset = selectedRange?.[0] ?? 0;
                if (start < end) {
                  onRangeChange([offset + start, offset + end]);
                }
              }}
            />}
          </BarChart>
        </ResponsiveContainer>
      </div>
      {forecast && <div className="portfolio-chart-legend"><span className="portfolio-chart-legend-fact">Факт</span><span className="portfolio-chart-legend-forecast">Прогноз до конца года: {money.format(forecast.totalValue)}</span></div>}
      {chartRows.length > 2 && <div className="portfolio-chart-help">Выберите снимки на шкале под графиком</div>}
      {selectedRange && <Button type="link" size="small" className="portfolio-chart-reset" onClick={() => onRangeChange(null)}>Показать все снимки</Button>}
    </div>
  );
}

function MetricCard({ title, children, hint }: { title: string; children: ReactNode; hint?: string }) {
  return (
    <div className="portfolio-metric-card">
      <Typography.Text type="secondary">{title}</Typography.Text>
      <div className="portfolio-metric-value">{children}</div>
      {hint && <Typography.Text type="secondary" className="portfolio-metric-hint">{hint}</Typography.Text>}
    </div>
  );
}

export default function PortfolioPage() {
  const [valuationForm] = Form.useForm<ValuationForm>();
  const [cashFlowForm] = Form.useForm<CashFlowForm>();
  const [valuations, setValuations] = useState<PortfolioValuation[]>([]);
  const [cashFlows, setCashFlows] = useState<PortfolioCashFlow[]>([]);
  const [cashBalances, setCashBalances] = useState<CashBalance[]>([]);
  const [summary, setSummary] = useState<PortfolioSummary>();
  const [editingValuationId, setEditingValuationId] = useState<string>();
  const [editingValuationValue, setEditingValuationValue] = useState<number>();
  const [editingCashFlowId, setEditingCashFlowId] = useState<string>();
  const [editingCashFlow, setEditingCashFlow] = useState<PortfolioCashFlow>();
  const [period, setPeriod] = useState<[Dayjs, Dayjs]>();
  const [result, setResult] = useState<PortfolioReturn>();
  const [calculationError, setCalculationError] = useState<string>();
  const [loading, setLoading] = useState(true);
  const [chartRange, setChartRange] = useState<[number, number] | null>(null);
  const { message } = AntdApp.useApp();

  const load = async () => {
    setLoading(true);
    try {
      const page = await portfolioReturnApi.getPage(dayjs().format("YYYY-MM-DD"));
      setValuations(page.valuations);
      setCashFlows(page.cashFlows);
      setCashBalances(page.balances);
      setSummary(page.summary);
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось загрузить данные");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const today = dayjs();
    setPeriod([today.startOf("year"), today]);
    valuationForm.setFieldsValue({ date: today });
    cashFlowForm.setFieldsValue({ date: today, type: "Deposit" });
    void load();
  }, []);

  useLayoutEffect(() => {
    if (editingCashFlow) {
      cashFlowForm.resetFields();
      cashFlowForm.setFieldsValue({
        date: dayjs(editingCashFlow.date),
        type: editingCashFlow.type,
        amount: editingCashFlow.amount,
        comment: editingCashFlow.comment
      });
    }
  }, [editingCashFlow, cashFlowForm]);

  const saveValuation = async (values: ValuationForm) => {
    try {
      await portfolioReturnApi.saveValuation({
        date: values.date.format("YYYY-MM-DD"),
        totalValue: values.totalValue,
        comment: values.comment ?? ""
      });
      message.success("Оценка сохранена");
      valuationForm.resetFields();
      await load();
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось сохранить оценку");
    }
  };

  const updateValuationValue = async (row: PortfolioValuation) => {
    if (editingValuationValue === undefined || !Number.isFinite(editingValuationValue) || editingValuationValue < 0) {
      message.error("Укажите корректную стоимость");
      return;
    }

    try {
      await portfolioReturnApi.saveValuation({
        date: row.date,
        totalValue: editingValuationValue,
        comment: row.comment ?? ""
      }, row.id);
      setEditingValuationId(undefined);
      setEditingValuationValue(undefined);
      message.success("Оценка обновлена");
      await load();
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось обновить оценку");
    }
  };

  const saveCashFlow = async (values: CashFlowForm) => {
    try {
      await portfolioReturnApi.saveCashFlow({
        date: values.date.format("YYYY-MM-DD"),
        type: values.type,
        amount: values.amount,
        comment: values.comment ?? ""
      }, editingCashFlowId);
      message.success(editingCashFlowId ? "Операция обновлена" : "Операция сохранена");
      cashFlowForm.resetFields();
      setEditingCashFlowId(undefined);
      setEditingCashFlow(undefined);
      await load();
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось сохранить операцию");
    }
  };

  const calculate = async () => {
    if (!period) {
      return;
    }

    try {
      setCalculationError(undefined);
      setResult(await portfolioReturnApi.calculate(period[0].format("YYYY-MM-DD"), period[1].format("YYYY-MM-DD")));
    } catch (error) {
      setResult(undefined);
      setCalculationError(error instanceof Error ? error.message : "Не удалось рассчитать доходность");
    }
  };

  const deleteValuation = async (id: string) => {
    await portfolioReturnApi.deleteValuation(id);
    await load();
  };

  const deleteCashFlow = async (id: string) => {
    await portfolioReturnApi.deleteCashFlow(id);
    await load();
  };

  const orderedValuations = useMemo(
    () => [...valuations].sort((left, right) => left.date.localeCompare(right.date)),
    [valuations]
  );
  const valuationHistory = useMemo(
    () => [...orderedValuations].reverse(),
    [orderedValuations]
  );
  const chartValuations = useMemo(() => {
    return buildCashFlowAdjustedValuations(orderedValuations, cashFlows);
  }, [cashFlows, orderedValuations]);
  const valuationColumns: ColumnsType<PortfolioValuation> = [
    { title: "Дата", dataIndex: "date", key: "date" },
    {
      title: "Стоимость",
      dataIndex: "totalValue",
      key: "totalValue",
      render: (value: number, row) => row.id === editingValuationId
        ? <InputNumber
            autoFocus
            min={0}
            precision={2}
            className="portfolio-valuation-edit-input"
            style={{ width: 220 }}
            value={editingValuationValue}
            onChange={(nextValue) => setEditingValuationValue(nextValue ?? undefined)}
          />
        : money.format(value)
    },
    { title: "Комментарий", dataIndex: "comment", key: "comment" },
    {
      title: "Действия",
      key: "actions",
      render: (_, row) => row.id === editingValuationId
        ? <Space>
            <Button size="small" type="primary" onClick={() => void updateValuationValue(row)}>Сохранить</Button>
            <Button size="small" onClick={() => {
              setEditingValuationId(undefined);
              setEditingValuationValue(undefined);
            }}>Отмена</Button>
          </Space>
        : <Space>
            <Button size="small" onClick={() => {
              setEditingValuationId(row.id);
              setEditingValuationValue(row.totalValue);
            }}>Изменить</Button>
            <Button size="small" danger onClick={() => void deleteValuation(row.id)}>Удалить</Button>
          </Space>
    }
  ];

  const cashFlowColumns: ColumnsType<PortfolioCashFlow> = [
    { title: "Дата", dataIndex: "date", key: "date" },
    {
      title: "Тип",
      dataIndex: "type",
      key: "type",
      render: (type: CashFlowType) => type === "Deposit" ? "Пополнение" : type === "Withdrawal" ? "Вывод" : "Налог"
    },
    { title: "Сумма", dataIndex: "amount", key: "amount", render: (value: number) => money.format(value) },
    { title: "Комментарий", dataIndex: "comment", key: "comment" },
    {
      title: "Действия",
      key: "actions",
      render: (_, row) => (
        <Space>
          <Button size="small" onClick={() => {
            setEditingCashFlowId(row.id);
            setEditingCashFlow(row);
          }}>Изменить</Button>
          <Button size="small" danger onClick={() => void deleteCashFlow(row.id)}>Удалить</Button>
        </Space>
      )
    }
  ];

  return (
    <Layout className="app-layout">
      <Content className="app-content">
        <Typography.Title level={1}>Портфель</Typography.Title>
        <Typography.Paragraph type="secondary">
          Общая картина портфеля: позиции, кеш и фонды ликвидности в рублях.
        </Typography.Paragraph>

        <Space orientation="vertical" size="large" style={{ width: "100%" }}>
          <Card
            title={(
              <div className="portfolio-chart-title">
                <span>Стоимость портфеля</span>
              </div>
            )}
            className="portfolio-overview-card">
            <div className="portfolio-metrics-row">
              <div className="portfolio-metrics-section">
                <MetricCard title="Общий объём">
                  {summary ? money.format(summary.totalValueRub) : "—"}
                  {summary && <div className="portfolio-total-breakdown">
                    <span><small>Позиции</small><strong>{money.format(summary.positionsValueRub)}</strong></span>
                    <span><small>Кеш</small><strong>{money.format(summary.cashValueRub)}</strong></span>
                    <span><small>Фонды</small><strong>{money.format(summary.moneyMarketFundsValueRub)}</strong></span>
                  </div>}
                </MetricCard>
              </div>
              <div className="portfolio-metrics-section">
                <MetricCard title="Кеш" hint="Текущий расчётный остаток">
                  {cashBalances.length === 0
                    ? "—"
                    : <div className="portfolio-cash-values">
                        {cashBalances.map((balance) => (
                          <span key={balance.currencyId}>{number.format(balance.amount)} {balance.currencyId}</span>
                        ))}
                      </div>}
                </MetricCard>
              </div>
              <div className="portfolio-metrics-section">
                <MetricCard title="Результат позиций" hint="Нереализованный результат открытых позиций">
                  {!summary
                    ? "—"
                    : <Typography.Text type={summary.unrealizedPnlRub >= 0 ? "success" : "danger"}>
                        {summary.unrealizedPnlRub >= 0 ? "+" : ""}{money.format(summary.unrealizedPnlRub)}
                      </Typography.Text>}
                </MetricCard>
              </div>
            </div>
            <div className="portfolio-chart-section">
              <ValuationChart
                rows={chartValuations}
                selectedRange={chartRange}
                onRangeChange={setChartRange}
              />
            </div>
          </Card>

          <Card title="Доходность портфеля" className="portfolio-return-card">
            <Space wrap>
               {period && <>
                 <DatePicker value={period[0]} onChange={(value) => value && setPeriod([value, period[1]])} />
                 <DatePicker value={period[1]} onChange={(value) => value && setPeriod([period[0], value])} />
               </>}
               <Button type="primary" disabled={!period} onClick={() => void calculate()}>Рассчитать</Button>
            </Space>
            {calculationError && <Alert type="error" showIcon title={calculationError} style={{ marginTop: 16 }} />}
            {result && <div className="portfolio-return-result">
              <Typography.Title level={2}>{percent.format(result.periodReturn)} за период</Typography.Title>
              <div className="portfolio-return-details">
                <span>Годовая доходность <strong>{percent.format(result.annualizedReturn)}</strong></span>
                <span>Финансовый результат <strong>{money.format(result.absoluteReturn)}</strong></span>
                <span>Стоимость <strong>{money.format(result.startValue)} → {money.format(result.endValue)}</strong></span>
                <span>Пополнения <strong>{money.format(result.deposits)}</strong></span>
                <span>Выводы <strong>{money.format(result.withdrawals)}</strong></span>
                <span>Налоги <strong>{money.format(result.taxes)}</strong></span>
              </div>
            </div>}
          </Card>

          <Collapse
            items={[
              {
                key: "valuations",
                label: "Оценки портфеля",
                forceRender: true,
                children: (
                  <Space orientation="vertical" size="large" style={{ width: "100%" }}>
                    <Card title="Добавить оценку портфеля">
                      <Form
                        form={valuationForm}
                        layout="inline"
                        onFinish={saveValuation}
                        >
                        <Form.Item name="date" rules={[{ required: true, message: "Укажите дату" }]}>
                          <DatePicker placeholder="Дата" />
                        </Form.Item>
                        <Form.Item name="totalValue" rules={[{ required: true, message: "Укажите стоимость" }]}>
                          <InputNumber min={0} precision={2} controls={false} className="portfolio-valuation-input" placeholder="Стоимость, ₽" />
                        </Form.Item>
                        <Form.Item name="comment"><Input placeholder="Комментарий" /></Form.Item>
                        <Form.Item><Button type="primary" htmlType="submit">Сохранить</Button></Form.Item>
                      </Form>
                    </Card>
                    <Card title="История оценок">
                      <Table rowKey="id" loading={loading} columns={valuationColumns} dataSource={valuationHistory} pagination={{ pageSize: 10 }} />
                    </Card>
                  </Space>
                )
              },
              {
                key: "cash-flows",
                label: "Движения денег",
                forceRender: true,
                children: (
                  <Space orientation="vertical" size="large" style={{ width: "100%" }}>
                    <Card title={editingCashFlowId ? "Изменить движение денег" : "Добавить пополнение или вывод"}>
                      <Form
                        key={editingCashFlowId ?? "new-cash-flow"}
                        form={cashFlowForm}
                        preserve={false}
                        layout="inline"
                        onFinish={saveCashFlow}
                        >
                        <Form.Item name="date" rules={[{ required: true, message: "Укажите дату" }]}>
                          <DatePicker placeholder="Дата" />
                        </Form.Item>
                        <Form.Item name="type" rules={[{ required: true, message: "Выберите тип" }]}>
                          <Select style={{ width: 150 }} options={[
                            { value: "Deposit", label: "Пополнение" },
                            { value: "Withdrawal", label: "Вывод" },
                            { value: "Tax", label: "Налог" }
                          ]} />
                        </Form.Item>
                        <Form.Item name="amount" rules={[{ required: true, message: "Укажите сумму" }]}>
                          <InputNumber min={0.01} precision={2} placeholder="Сумма, ₽" />
                        </Form.Item>
                        <Form.Item name="comment"><Input placeholder="Комментарий" /></Form.Item>
                        <Form.Item><Button type="primary" htmlType="submit">Сохранить</Button></Form.Item>
                        {editingCashFlowId && <Form.Item><Button onClick={() => { setEditingCashFlowId(undefined); setEditingCashFlow(undefined); cashFlowForm.resetFields(); }}>Отмена</Button></Form.Item>}
                      </Form>
                    </Card>
                    <Card title="История движений">
                      <Table rowKey="id" loading={loading} columns={cashFlowColumns} dataSource={cashFlows} pagination={{ pageSize: 10 }} />
                    </Card>
                  </Space>
                )
              }
            ]}
          />
        </Space>
      </Content>
    </Layout>
  );
}
