"use client";

import { useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
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
  Tabs,
  Table,
  Typography,
} from "antd";
import type { ColumnsType } from "antd/es/table";
import {
  hasCurrentBenchmarkContract,
  portfolioReturnApi,
  type CashFlowType,
  type PortfolioBenchmarkResult,
  type PortfolioCashFlow,
  type PortfolioReturn,
  type PortfolioValuation
} from "../../lib/portfolio-return-api";
import {
  getPortfolioComparisonUnavailableHint,
  isBenchmarkEndDateDisabled,
  isBenchmarkStartDateDisabled
} from "../../lib/portfolio-benchmark-presentation";
import type { CashBalance, PortfolioSummary } from "../../lib/portfolio-ledger-api";
import {
  Bar,
  BarChart,
  Brush,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
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
const formatBenchmarkPercent = (value: number | null | undefined) =>
  value == null || !Number.isFinite(value) ? "—" : percent.format(value);
const formatBenchmarkDifference = (value: number | null | undefined, base: number | null | undefined) => {
  if (value == null || base == null || !Number.isFinite(value) || !Number.isFinite(base)) {
    return "—";
  }

  const difference = (value - base) * 100;
  return `${difference > 0 ? "+" : ""}${number.format(difference)} п.п.`;
};
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
  const [hasChartLayout, setHasChartLayout] = useState(false);

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
  const chartDomainKey = chartData.map((point) => point.id).join("|");
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
        <ResponsiveContainer
          width="100%"
          height="100%"
          onResize={(width, height) => setHasChartLayout(
            Number.isFinite(width) && width > 0 && Number.isFinite(height) && height > 0
          )}
        >
          <BarChart key={chartDomainKey} data={chartData} margin={{ top: 18, right: 18, bottom: 10, left: 8 }} barCategoryGap="8%" barGap={0}>
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
            {hasChartLayout && chartRows.length > 2 && <Brush
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

type BenchmarkChartRow = {
  date: string;
  timestamp: number;
  portfolioProfitPercent?: number | null;
  portfolioTwrPercent?: number | null;
  lqdtProfitPercent?: number | null;
  lqdtTwrPercent?: number | null;
  indexedBondProfitPercent?: number | null;
  indexedBondTwrPercent?: number | null;
  [series: string]: string | number | null | undefined;
};

type BenchmarkSeriesPoint = {
  date: string;
  timestamp: number;
  profitPercent?: number | null;
  twrPercent?: number | null;
};

function BenchmarkChart({ result }: { result: PortfolioBenchmarkResult }) {
  const [brushRange, setBrushRange] = useState<{ startIndex: number; endIndex: number }>();
  const [visibleIndexes, setVisibleIndexes] = useState<string[]>(["lqdt", "indexedBond"]);
  const [metric, setMetric] = useState<"profit" | "twr">("profit");
  const [hasChartLayout, setHasChartLayout] = useState(false);
  const [chartWidth, setChartWidth] = useState(0);
  const sideLegend = chartWidth > 800;
  const chartData = useMemo(() => {
    const rowsByDate = new Map<string, BenchmarkChartRow>();
    const addPoint = (date: string) => {
      let row = rowsByDate.get(date);
      if (!row) {
        row = { date, timestamp: dayjs(date).valueOf() };
        rowsByDate.set(date, row);
      }
      return row;
    };

    result.portfolioPoints.forEach((point) => {
      const row = addPoint(point.date);
      row.portfolioProfitPercent = point.profitPercent;
      row.portfolioTwrPercent = point.twrPercent;
    });
    result.lqdtPoints.forEach((point) => {
      const row = addPoint(point.date);
      row.lqdtProfitPercent = point.profitPercent;
      row.lqdtTwrPercent = point.twrPercent;
    });
    result.indexedBondPoints?.forEach((point) => {
      const row = addPoint(point.date);
      row.indexedBondProfitPercent = point.profitPercent;
      row.indexedBondTwrPercent = point.twrPercent;
    });

    return [...rowsByDate.values()].sort((left, right) => left.timestamp - right.timestamp);
  }, [result]);
  const portfolioSeriesData = useMemo(() => chartData
    .filter((row) => Object.prototype.hasOwnProperty.call(row, "portfolioProfitPercent"))
    .map((row): BenchmarkSeriesPoint => ({
      date: row.date,
      timestamp: row.timestamp,
      profitPercent: row.portfolioProfitPercent,
      twrPercent: row.portfolioTwrPercent
    })), [chartData]);
  const lqdtSeriesData = useMemo(() => chartData
    .filter((row) => Object.prototype.hasOwnProperty.call(row, "lqdtProfitPercent"))
    .map((row): BenchmarkSeriesPoint => ({
      date: row.date,
      timestamp: row.timestamp,
      profitPercent: row.lqdtProfitPercent,
      twrPercent: row.lqdtTwrPercent
    })), [chartData]);
  const indexedBondSeriesData = useMemo(() => chartData
    .filter((row) => Object.prototype.hasOwnProperty.call(row, "indexedBondProfitPercent"))
    .map((row): BenchmarkSeriesPoint => ({
      date: row.date,
      timestamp: row.timestamp,
      profitPercent: row.indexedBondProfitPercent,
      twrPercent: row.indexedBondTwrPercent
    })), [chartData]);
  const selectedStartIndex = Math.min(brushRange?.startIndex ?? 0, chartData.length - 1);
  const selectedEndIndex = Math.max(
    selectedStartIndex,
    Math.min(brushRange?.endIndex ?? chartData.length - 1, chartData.length - 1)
  );
  const isZoomed = selectedStartIndex > 0 || selectedEndIndex < chartData.length - 1;
  const selectedStartTimestamp = chartData[selectedStartIndex]?.timestamp ?? 0;
  const selectedEndTimestamp = chartData[selectedEndIndex]?.timestamp ?? selectedStartTimestamp;
  const sliceSeriesToBrush = (points: BenchmarkSeriesPoint[]) => {
    if (!isZoomed || points.length < 2) return points;
    let firstVisibleIndex = points.findIndex((point) => point.timestamp >= selectedStartTimestamp);
    if (firstVisibleIndex < 0) firstVisibleIndex = points.length - 1;
    const lastVisibleIndex = points.findLastIndex((point) => point.timestamp <= selectedEndTimestamp);
    const start = Math.max(0, firstVisibleIndex - 1);
    const end = Math.min(points.length - 1, Math.max(firstVisibleIndex, lastVisibleIndex) + 1);
    return points.slice(start, end + 1);
  };
  const visiblePortfolioSeriesData = useMemo(() => sliceSeriesToBrush(portfolioSeriesData),
    [portfolioSeriesData, isZoomed, selectedStartTimestamp, selectedEndTimestamp]);
  const visibleLqdtSeriesData = useMemo(() => sliceSeriesToBrush(lqdtSeriesData),
    [lqdtSeriesData, isZoomed, selectedStartTimestamp, selectedEndTimestamp]);
  const visibleIndexedBondSeriesData = useMemo(() => sliceSeriesToBrush(indexedBondSeriesData),
    [indexedBondSeriesData, isZoomed, selectedStartTimestamp, selectedEndTimestamp]);
  const renderTooltip = ({ active, payload }: TooltipContentProps) => {
    if (!active || !payload?.length) {
      return null;
    }

    const point = payload[0]?.payload as BenchmarkChartRow | undefined;
    if (!point) {
      return null;
    }

    return (
      <div className="portfolio-chart-tooltip-card">
        <strong>Дата: {dayjs(point.date).format("DD.MM.YYYY")}</strong>
        {payload.map((entry) => entry.value == null ? null : (
          <span key={`${entry.name}-${String(entry.dataKey)}`} style={{ color: entry.color }}>
            {entry.name}: {percent.format(Number(entry.value))}
          </span>
        ))}
      </div>
    );
  };

  if (chartData.length < 2) {
    return <div className="portfolio-chart-empty">Для сравнения недостаточно точек истории.</div>;
  }

  return (
    <div className="portfolio-chart-wrap" role="region" aria-label="Сравнение доходности портфеля и индексов">
      <div className="portfolio-benchmark-series-select">
        <Typography.Text type="secondary">Индексы на графике</Typography.Text>
        <Select
          mode="multiple"
          aria-label="Индексы на графике"
          value={visibleIndexes.filter((index) => index === "lqdt" || (result.indexedBondPoints?.length ?? 0) > 0)}
          options={[
            { label: "LQDT", value: "lqdt" },
            { label: "RU000A10F504", value: "indexedBond", disabled: (result.indexedBondPoints?.length ?? 0) === 0 }
          ]}
          onChange={setVisibleIndexes}
          maxTagCount={2}
          style={{ minWidth: 240, maxWidth: "100%" }}
        />
        <Typography.Text type="secondary">Показатель</Typography.Text>
        <Select
          aria-label="Показатель графика индексов"
          value={metric}
          options={[
            { label: "Прибыль на вложенные средства", value: "profit" },
            { label: "TWR", value: "twr" }
          ]}
          onChange={setMetric}
          style={{ minWidth: 230, maxWidth: "100%" }}
        />
      </div>
      <div className="portfolio-chart-recharts">
        <ResponsiveContainer
          width="100%"
          height="100%"
          onResize={(width, height) => {
            setHasChartLayout(Number.isFinite(width) && width > 0 && Number.isFinite(height) && height > 0);
            setChartWidth(width);
          }}
        >
          <LineChart data={chartData} margin={{ top: 18, right: sideLegend ? 76 : 18, bottom: 10, left: 8 }}>
            <CartesianGrid className="portfolio-chart-grid" vertical={false} />
            <XAxis
              dataKey="timestamp"
              type="number"
              domain={isZoomed ? [selectedStartTimestamp, selectedEndTimestamp] : ["dataMin", "dataMax"]}
              allowDataOverflow
              tickFormatter={(value: number) => dayjs(value).format("MM.YYYY")}
              tick={{ fill: "rgba(255, 255, 255, 0.55)", fontSize: 11 }}
              tickLine={false}
              axisLine={false}
              minTickGap={24}
              tickCount={6}
            />
            <YAxis
              type="number"
              domain={["auto", "auto"]}
              tickFormatter={(value: number) => percent.format(value)}
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
            <Legend
              layout={sideLegend ? "vertical" : "horizontal"}
              align={sideLegend ? "right" : "center"}
              verticalAlign={sideLegend ? "middle" : "bottom"}
              width={sideLegend ? 190 : undefined}
              wrapperStyle={{ fontSize: 12, lineHeight: "24px" }}
            />
            {visiblePortfolioSeriesData.length > 0 && <Line
              data={visiblePortfolioSeriesData}
              type="linear"
              dataKey={metric === "profit" ? "profitPercent" : "twrPercent"}
              name="Портфель"
              stroke="#1677ff"
              strokeWidth={2}
              dot={false}
              connectNulls={false}
              isAnimationActive={false}
            />}
            {visibleIndexes.includes("indexedBond") && (result.indexedBondPoints?.length ?? 0) > 0 && <Line
              data={visibleIndexedBondSeriesData}
              type="linear"
              dataKey={metric === "profit" ? "profitPercent" : "twrPercent"}
              name="RU000A10F504"
              stroke="#faad14"
              strokeWidth={2}
              dot={false}
              connectNulls={false}
              isAnimationActive={false}
            />}
            {visibleIndexes.includes("lqdt") && <Line
              data={visibleLqdtSeriesData}
              type="linear"
              dataKey={metric === "profit" ? "profitPercent" : "twrPercent"}
              name="LQDT"
              stroke="#52c41a"
              strokeWidth={2}
              dot={false}
              connectNulls={false}
              isAnimationActive={false}
            />}
            {hasChartLayout && chartData.length > 2 && <Brush
              dataKey="date"
              ariaLabel="Перетащите границы, чтобы выбрать период графика"
              height={40}
              travellerWidth={24}
              stroke="#1677ff"
              fill="#141414"
              startIndex={selectedStartIndex}
              endIndex={selectedEndIndex}
              tickFormatter={(value) => dayjs(String(value)).format("MM.YYYY")}
              onChange={({ startIndex = 0, endIndex = chartData.length - 1 }) => {
                const start = Number.isFinite(startIndex)
                  ? Math.min(Math.max(0, startIndex), chartData.length - 1)
                  : 0;
                const end = Number.isFinite(endIndex)
                  ? Math.min(Math.max(start, endIndex), chartData.length - 1)
                  : chartData.length - 1;
                setBrushRange({ startIndex: start, endIndex: end });
              }}
            />}
          </LineChart>
        </ResponsiveContainer>
      </div>
      {chartData.length > 2 && <div className="portfolio-chart-help">Тяните края выделения, чтобы менять даты; середину — чтобы прокручивать период</div>}
      {isZoomed && <Button
        type="link"
        size="small"
        className="portfolio-chart-reset"
        onClick={() => setBrushRange({ startIndex: 0, endIndex: chartData.length - 1 })}
      >
        Показать весь период
      </Button>}
      <div className="portfolio-benchmark-chart-caption">
        {metric === "profit"
          ? "Портфель: прибыль на вложенные средства · инструменты: доходность удержания по ценам"
          : "Портфель: TWR после налогов · инструменты: доходность удержания без потоков и налогов"}
      </div>
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
  const [benchmarkPeriod, setBenchmarkPeriod] = useState<[Dayjs, Dayjs]>();
  const [benchmarkTabOpened, setBenchmarkTabOpened] = useState(false);
  const autoBenchmarkCalculationStarted = useRef(false);
  const [result, setResult] = useState<PortfolioReturn>();
  const [calculationError, setCalculationError] = useState<string>();
  const [benchmarkResult, setBenchmarkResult] = useState<PortfolioBenchmarkResult>();
  const [benchmarkCalculationError, setBenchmarkCalculationError] = useState<string>();
  const [benchmarkLoading, setBenchmarkLoading] = useState(false);
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
    setBenchmarkPeriod([today.startOf("year"), today]);
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

  const calculateBenchmark = async (selectedPeriod: [Dayjs, Dayjs] | undefined = benchmarkPeriod) => {
    if (!selectedPeriod) {
      return;
    }

    try {
      setBenchmarkLoading(true);
      setBenchmarkCalculationError(undefined);
      setBenchmarkResult(await portfolioReturnApi.calculateBenchmark(
        selectedPeriod[0].format("YYYY-MM-DD"),
        selectedPeriod[1].format("YYYY-MM-DD")
      ));
    } catch (error) {
      setBenchmarkResult(undefined);
      setBenchmarkCalculationError(error instanceof Error ? error.message : "Не удалось рассчитать сравнение с индексами");
    } finally {
      setBenchmarkLoading(false);
    }
  };

  const changePeriod = (start: Dayjs | null, end: Dayjs | null) => {
    if (start && end) {
      setPeriod([start, end]);
      setResult(undefined);
      setCalculationError(undefined);
    }
  };

  const changeBenchmarkPeriod = (start: Dayjs | null, end: Dayjs | null) => {
    if (start && end) {
      setBenchmarkPeriod([start, end]);
      setBenchmarkResult(undefined);
      setBenchmarkCalculationError(undefined);
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
  useEffect(() => {
    if (!benchmarkTabOpened || loading || autoBenchmarkCalculationStarted.current || orderedValuations.length === 0) {
      return;
    }

    const start = dayjs().startOf("year");
    const end = dayjs(orderedValuations.at(-1)!.date);
    const selectedPeriod: [Dayjs, Dayjs] = [start, end];
    autoBenchmarkCalculationStarted.current = true;
    setBenchmarkPeriod(selectedPeriod);
    void calculateBenchmark(selectedPeriod);
  }, [benchmarkTabOpened, loading, orderedValuations, calculateBenchmark]);
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
          <Tabs
            className="portfolio-main-tabs"
              defaultActiveKey="value"
              onChange={(activeKey) => {
                if (activeKey === "benchmark") {
                  setBenchmarkTabOpened(true);
                }
              }}
              items={[
                {
                  key: "value",
                  label: "Стоимость портфеля",
                  children: (
                    <Card className="portfolio-overview-card">
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
                            {!summary || summary.unrealizedPnlRub == null
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
                  )
                },
                {
                  key: "benchmark",
                  label: "Индексы",
                  children: (
                    <Card className="portfolio-overview-card">
                      <div className="portfolio-benchmark-content">
                      <Space wrap className="portfolio-benchmark-controls">
                        {benchmarkPeriod && <>
                          <DatePicker
                            aria-label="Начало периода сравнения"
                            value={benchmarkPeriod[0]}
                            disabledDate={(value) => isBenchmarkStartDateDisabled(value, benchmarkPeriod[1])}
                            onChange={(value) => changeBenchmarkPeriod(value, benchmarkPeriod[1])}
                          />
                          <DatePicker
                            aria-label="Конец периода сравнения"
                            value={benchmarkPeriod[1]}
                            disabledDate={(value) => isBenchmarkEndDateDisabled(value, benchmarkPeriod[0])}
                            onChange={(value) => changeBenchmarkPeriod(benchmarkPeriod[0], value)}
                          />
                        </>}
                        <Button
                          type="primary"
                          disabled={!benchmarkPeriod}
                          loading={benchmarkLoading}
                          onClick={() => void calculateBenchmark()}
                        >
                          Сравнить
                        </Button>
                      </Space>
                      {benchmarkCalculationError && <Alert
                        type="error"
                        showIcon
                        title={benchmarkCalculationError}
                        style={{ marginTop: 16 }}
                      />}
                      {benchmarkResult && !hasCurrentBenchmarkContract(benchmarkResult) ? <Alert
                        type="warning"
                        showIcon
                        title="Сервер вернул несовместимый формат сравнения. Обновите backend."
                        style={{ marginTop: 16 }}
                      /> : benchmarkResult ? <>
                        <div className="portfolio-benchmark-metrics">
                          <MetricCard
                            title="Фактический портфель · прибыль на вложенные средства"
                            hint={benchmarkResult.portfolioUnavailableReason ?? `TWR после фактических налогов: ${formatBenchmarkPercent(benchmarkResult.portfolioTwrPercent)}`}
                          >
                            {formatBenchmarkPercent(benchmarkResult.portfolioProfitPercent)}
                          </MetricCard>
                          <MetricCard
                            title="LQDT · рост цены пая"
                            hint={getPortfolioComparisonUnavailableHint(benchmarkResult.portfolioUnavailableReason) ?? (
                              benchmarkResult.lqdtStartObservationDate === benchmarkResult.from
                                ? `Без потоков, налогов, брокерских комиссий и спреда · LQDT − TWR портфеля: ${formatBenchmarkDifference(benchmarkResult.lqdtTwrPercent, benchmarkResult.portfolioTwrPercent)}`
                                : `LQDT: ${dayjs(benchmarkResult.lqdtStartObservationDate).format("DD.MM.YYYY")}–${dayjs(benchmarkResult.to).format("DD.MM.YYYY")}; портфель: ${dayjs(benchmarkResult.from).format("DD.MM.YYYY")}–${dayjs(benchmarkResult.to).format("DD.MM.YYYY")}. Периоды различаются.`
                            )}
                          >
                            {formatBenchmarkPercent(benchmarkResult.lqdtProfitPercent)}
                          </MetricCard>
                          <MetricCard
                            title={`RU000A10F504 · линия с ${benchmarkResult.indexedBondComparisonStartDate
                              ? dayjs(benchmarkResult.indexedBondComparisonStartDate).format("DD.MM.YYYY")
                              : "нет данных"}`}
                            hint={benchmarkResult.indexedBondProfitPercent == null
                              ? benchmarkResult.indexedBondUnavailableReason ?? "Недостаточно исторических данных"
                              : getPortfolioComparisonUnavailableHint(benchmarkResult.portfolioUnavailableReason) ??
                                `Доходность удержания без потоков и налогов · RU − TWR портфеля: ${formatBenchmarkDifference(benchmarkResult.indexedBondTwrPercent, benchmarkResult.portfolioTwrPercent)}`}
                          >
                            {benchmarkResult.indexedBondProfitPercent == null
                              ? "—"
                              : formatBenchmarkPercent(benchmarkResult.indexedBondProfitPercent)}
                          </MetricCard>
                        </div>
                        {benchmarkResult.portfolioUnavailableReason && <Alert
                          type="warning"
                          showIcon
                          title={`Фактический портфель не рассчитан: ${benchmarkResult.portfolioUnavailableReason}`}
                          style={{ marginTop: 12 }}
                        />}
                        <div className="portfolio-benchmark-observations">
                          <span>
                            Цены LQDT: данные с {dayjs(benchmarkResult.lqdtStartObservationDate).format("DD.MM.YYYY")} по {dayjs(benchmarkResult.lqdtEndObservationDate).format("DD.MM.YYYY")}
                          </span>
                          {benchmarkResult.indexedBondStartObservationDate && benchmarkResult.indexedBondEndObservationDate && <span>
                            RU000A10F504: цены с {dayjs(benchmarkResult.indexedBondStartObservationDate).format("DD.MM.YYYY")} по {dayjs(benchmarkResult.indexedBondEndObservationDate).format("DD.MM.YYYY")}{benchmarkResult.indexedBondComparisonStartDate && `; линия с ${dayjs(benchmarkResult.indexedBondComparisonStartDate).format("DD.MM.YYYY")}`}
                          </span>}
                        </div>
                        {benchmarkResult.indexedBondUnavailableReason && <Alert
                          type="warning"
                          showIcon
                          title={benchmarkResult.indexedBondUnavailableReason}
                          style={{ marginTop: 12 }}
                        />}
                        <div className="portfolio-chart-section">
                          <BenchmarkChart result={benchmarkResult} />
                        </div>
                        <Typography.Paragraph type="secondary" className="portfolio-benchmark-note">
                          Фактический портфель учитывает сохранённые пополнения, выводы и налоговые списания. Для LQDT считается доходность удержания по официальной цене закрытия основной сессии MOEX LEGALCLOSEPRICE: (последняя цена в выбранном периоде / первая цена в выбранном периоде) − 1. Начало линии — первая торговая дата не раньше «с», конец — последняя торговая дата не позже «по»; если последний день не торговый, последнее значение переносится на него. В гипотетических линиях инструментов пополнения, выводы, налоги, брокерские комиссии и спред не моделируются; расходы фонда, отражённые в цене пая, сохраняются. Это доходность по официальным ценам закрытия пая LQDT, не расчёт по индикатору LQDTM. Для RU000A10F504 стоимость включает чистую цену, исторический номинал и НКД; между торговыми датами НКД начисляется ежедневно как «номинал × ставка купона / 100 / 365», выплаченные купоны остаются деньгами без реинвестирования. TWR портфеля рассчитывается по фактическим оценкам и нейтрализует пополнения и выводы, но включает налоговые списания. Все проценты не годовые.
                        </Typography.Paragraph>
                      </> : !benchmarkCalculationError && <div className="portfolio-chart-empty portfolio-benchmark-empty">
                        Выберите период и нажмите «Сравнить».
                      </div>}
                      </div>
                    </Card>
                  )
                }
              ]}
          />

          <Card title="Доходность портфеля" className="portfolio-return-card">
            <Space wrap>
               {period && <>
                 <DatePicker value={period[0]} onChange={(value) => changePeriod(value, period[1])} />
                 <DatePicker value={period[1]} onChange={(value) => changePeriod(period[0], value)} />
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
