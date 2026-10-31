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
  Descriptions,
  Form,
  Grid,
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
import { getHorizontalScrollAdjustment } from "../../lib/portfolio-tab-navigation";
import {
  isBenchmarkEndDateDisabled,
  isBenchmarkStartDateDisabled
} from "../../lib/portfolio-benchmark-presentation";
import type { PortfolioSummary } from "../../lib/portfolio-ledger-api";
import PortfolioCurrencyAnalyticsTab from "./currency-analytics-tab";
import {
  Bar,
  BarChart,
  Brush,
  CartesianGrid,
  Cell,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
  usePlotArea,
  useXAxisScale,
  useYAxisScale,
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
  lqdtProfitPercent?: number | null;
  tmonProfitPercent?: number | null;
  indexedBondProfitPercent?: number | null;
  [series: string]: string | number | null | undefined;
};

type BenchmarkSeriesPoint = {
  date: string;
  timestamp: number;
  profitPercent?: number | null;
};

type BenchmarkEndLabel = { key: string; name: string; color: string; value: number; timestamp: number };

function BenchmarkEndLabels({ entries, from, to }: { entries: BenchmarkEndLabel[]; from: number; to: number }) {
  const plot = usePlotArea();
  const xScale = useXAxisScale();
  const yScale = useYAxisScale();
  if (!plot || !xScale || !yScale) return null;

  const top = plot.y + 10;
  const bottom = plot.y + plot.height - 10;
  const labels = entries.flatMap((entry) => {
    if (entry.timestamp < from || entry.timestamp > to) return [];
    const x = xScale(entry.timestamp);
    const y = yScale(entry.value);
    return x != null && y != null && Number.isFinite(x) && Number.isFinite(y)
      ? [{ ...entry, x, y, labelY: Math.max(top, Math.min(bottom, y)) }]
      : [];
  });
  for (let index = 1; index < labels.length; index++) {
    labels[index].labelY = Math.max(labels[index].labelY, labels[index - 1].labelY + 22);
  }
  if (labels.length > 0) {
    labels[labels.length - 1].labelY = Math.min(bottom, labels[labels.length - 1].labelY);
    for (let index = labels.length - 2; index >= 0; index--) {
      labels[index].labelY = Math.min(labels[index].labelY, labels[index + 1].labelY - 22);
    }
  }
  const labelX = plot.x + plot.width + 10;
  return <g pointerEvents="none">
    {labels.map((entry) => <g key={entry.key}>
      <circle cx={entry.x} cy={entry.y} r={3} fill={entry.color} />
      <path d={`M ${entry.x} ${entry.y} L ${labelX - 4} ${entry.labelY}`} stroke={entry.color} fill="none" opacity={0.65} />
      <text x={labelX} y={entry.labelY} dominantBaseline="middle" fill={entry.color} fontSize={12} fontWeight={600} stroke="#141414" strokeWidth={3} paintOrder="stroke">
        {entry.name} {formatBenchmarkPercent(entry.value)}
      </text>
    </g>)}
  </g>;
}

function BenchmarkChart({
  result,
  visibleIndexes,
  onVisibleIndexesChange
}: {
  result: PortfolioBenchmarkResult;
  visibleIndexes: string[];
  onVisibleIndexesChange: (indexes: string[]) => void;
}) {
  const [brushRange, setBrushRange] = useState<{ startIndex: number; endIndex: number }>();
  const [hasChartLayout, setHasChartLayout] = useState(false);
  const isCompactChart = !Grid.useBreakpoint().md;
  const endLabels = [
    { key: "portfolio", name: "Портфель", color: "#1677ff", point: result.portfolioPoints.at(-1), visible: true },
    { key: "lqdt", name: "LQDT", color: "#52c41a", point: result.lqdtPoints.at(-1), visible: visibleIndexes.includes("lqdt") },
    { key: "tmon", name: "TMON", color: "#13c2c2", point: result.tmonPoints.at(-1), visible: visibleIndexes.includes("tmon") },
    { key: "indexedBond", name: "ВЭБ-58", color: "#faad14", point: result.indexedBondPoints?.at(-1), visible: visibleIndexes.includes("indexedBond") }
  ].flatMap((entry): BenchmarkEndLabel[] => {
    const point = entry.point;
    if (!entry.visible || point?.profitPercent == null || !Number.isFinite(point.profitPercent)) return [];
    return [{ key: entry.key, name: entry.name, color: entry.color, value: point.profitPercent, timestamp: dayjs(point.date).valueOf() }];
  }).sort((left, right) => right.value - left.value);
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
    });
    result.lqdtPoints.forEach((point) => {
      const row = addPoint(point.date);
      row.lqdtProfitPercent = point.profitPercent;
    });
    result.tmonPoints.forEach((point) => {
      const row = addPoint(point.date);
      row.tmonProfitPercent = point.profitPercent;
    });
    result.indexedBondPoints?.forEach((point) => {
      const row = addPoint(point.date);
      row.indexedBondProfitPercent = point.profitPercent;
    });

    return [...rowsByDate.values()].sort((left, right) => left.timestamp - right.timestamp);
  }, [result]);
  const chartTimelineKey = useMemo(() => chartData.map((point) => point.date).join("|"), [chartData]);
  useEffect(() => setBrushRange(undefined), [chartTimelineKey]);
  const portfolioSeriesData = useMemo(() => chartData
    .filter((row) => Object.prototype.hasOwnProperty.call(row, "portfolioProfitPercent"))
    .map((row): BenchmarkSeriesPoint => ({
      date: row.date,
      timestamp: row.timestamp,
      profitPercent: row.portfolioProfitPercent
    })), [chartData]);
  const lqdtSeriesData = useMemo(() => chartData
    .filter((row) => Object.prototype.hasOwnProperty.call(row, "lqdtProfitPercent"))
    .map((row): BenchmarkSeriesPoint => ({
      date: row.date,
      timestamp: row.timestamp,
      profitPercent: row.lqdtProfitPercent
    })), [chartData]);
  const tmonSeriesData = useMemo(() => chartData
    .filter((row) => Object.prototype.hasOwnProperty.call(row, "tmonProfitPercent"))
    .map((row): BenchmarkSeriesPoint => ({
      date: row.date,
      timestamp: row.timestamp,
      profitPercent: row.tmonProfitPercent
    })), [chartData]);
  const indexedBondSeriesData = useMemo(() => chartData
    .filter((row) => Object.prototype.hasOwnProperty.call(row, "indexedBondProfitPercent"))
    .map((row): BenchmarkSeriesPoint => ({
      date: row.date,
      timestamp: row.timestamp,
      profitPercent: row.indexedBondProfitPercent
    })), [chartData]);
  const selectedStartIndex = Math.min(brushRange?.startIndex ?? 0, chartData.length - 1);
  const selectedEndIndex = Math.max(
    selectedStartIndex,
    Math.min(brushRange?.endIndex ?? chartData.length - 1, chartData.length - 1)
  );
  const isZoomed = selectedStartIndex > 0 || selectedEndIndex < chartData.length - 1;
  const selectedStartTimestamp = chartData[selectedStartIndex]?.timestamp ?? 0;
  const selectedEndTimestamp = chartData[selectedEndIndex]?.timestamp ?? selectedStartTimestamp;
  const summaryEntries = [
    { key: "indexedBond", name: "ВЭБ-58", color: "#faad14", points: indexedBondSeriesData, visible: visibleIndexes.includes("indexedBond") },
    { key: "lqdt", name: "LQDT", color: "#52c41a", points: lqdtSeriesData, visible: visibleIndexes.includes("lqdt") },
    { key: "tmon", name: "TMON", color: "#13c2c2", points: tmonSeriesData, visible: visibleIndexes.includes("tmon") },
    { key: "portfolio", name: "Портфель", color: "#1677ff", points: portfolioSeriesData, visible: true }
  ].flatMap((series): BenchmarkEndLabel[] => {
    if (!series.visible) return [];
    const point = series.points.findLast((item) =>
      item.timestamp >= selectedStartTimestamp && item.timestamp <= selectedEndTimestamp && item.profitPercent != null
    );
    return point?.profitPercent == null
      ? []
      : [{ key: series.key, name: series.name, color: series.color, value: point.profitPercent, timestamp: point.timestamp }];
  });
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
  const visibleTmonSeriesData = useMemo(() => sliceSeriesToBrush(tmonSeriesData),
    [tmonSeriesData, isZoomed, selectedStartTimestamp, selectedEndTimestamp]);
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
          value={visibleIndexes}
          options={[
            { label: "LQDT", value: "lqdt" },
            { label: "TMON", value: "tmon" },
            { label: "RU000A10F504", value: "indexedBond", disabled: (result.indexedBondPoints?.length ?? 0) === 0 }
          ]}
          onChange={onVisibleIndexesChange}
          maxTagCount={2}
        />
      </div>
      <div className="portfolio-chart-recharts">
        <ResponsiveContainer
          width="100%"
          height="100%"
          onResize={(width, height) => {
            setHasChartLayout(Number.isFinite(width) && width > 0 && Number.isFinite(height) && height > 0);
          }}
        >
          <LineChart key={chartTimelineKey} data={chartData} margin={isCompactChart
            ? { top: 12, right: 12, bottom: 8, left: 4 }
            : { top: 18, right: 170, bottom: 10, left: 8 }}>
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
            {visiblePortfolioSeriesData.length > 0 && <Line
              data={visiblePortfolioSeriesData}
              type="linear"
              dataKey="profitPercent"
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
              dataKey="profitPercent"
              name="ВЭБ-58"
              stroke="#faad14"
              strokeWidth={2}
              dot={false}
              connectNulls={false}
              isAnimationActive={false}
            />}
            {visibleIndexes.includes("lqdt") && <Line
              data={visibleLqdtSeriesData}
              type="linear"
              dataKey="profitPercent"
              name="LQDT"
              stroke="#52c41a"
              strokeWidth={2}
              dot={false}
              connectNulls={false}
              isAnimationActive={false}
            />}
            {visibleIndexes.includes("tmon") && result.tmonPoints.length > 0 && <Line
              data={visibleTmonSeriesData}
              type="linear"
              dataKey="profitPercent"
              name="TMON"
              stroke="#13c2c2"
              strokeWidth={2}
              dot={false}
              connectNulls={false}
              isAnimationActive={false}
            />}
            {!isCompactChart && <BenchmarkEndLabels entries={endLabels} from={selectedStartTimestamp} to={selectedEndTimestamp} />}
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
      <Descriptions className="portfolio-benchmark-mobile-summary" column={1} size="small" bordered>
        {summaryEntries.map((entry) => (
          <Descriptions.Item
            key={entry.key}
            label={(
              <Space size={10}>
                <span className="portfolio-benchmark-summary-dot" style={{ backgroundColor: entry.color }} aria-hidden="true" />
                <Typography.Text>{entry.name}</Typography.Text>
              </Space>
            )}
          >
            <Typography.Text className="portfolio-benchmark-summary-value" style={{ color: entry.color }}>
              {formatBenchmarkPercent(entry.value)}
            </Typography.Text>
          </Descriptions.Item>
        ))}
      </Descriptions>
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
        Портфель: прибыль на вложенные средства · LQDT, TMON и RU000A10F504: доходность удержания за выбранный период
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
  const [summary, setSummary] = useState<PortfolioSummary>();
  const [editingValuationId, setEditingValuationId] = useState<string>();
  const [editingValuationValue, setEditingValuationValue] = useState<number>();
  const [editingCashFlowId, setEditingCashFlowId] = useState<string>();
  const [editingCashFlow, setEditingCashFlow] = useState<PortfolioCashFlow>();
  const [period, setPeriod] = useState<[Dayjs, Dayjs]>();
  const [result, setResult] = useState<PortfolioReturn>();
  const [calculationError, setCalculationError] = useState<string>();
  const [benchmarkPeriod, setBenchmarkPeriod] = useState<[Dayjs, Dayjs]>();
  const [benchmarkTabOpened, setBenchmarkTabOpened] = useState(false);
  const autoBenchmarkCalculationStarted = useRef(false);
  const [benchmarkResult, setBenchmarkResult] = useState<PortfolioBenchmarkResult>();
  const [visibleBenchmarkIndexes, setVisibleBenchmarkIndexes] = useState<string[]>(["lqdt", "tmon", "indexedBond"]);
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

  const calculateBenchmark = async (
    selectedPeriod: [Dayjs, Dayjs] | undefined = benchmarkPeriod,
    selectedIndexes: string[] = visibleBenchmarkIndexes
  ) => {
    if (!selectedPeriod) {
      return;
    }

    try {
      setBenchmarkLoading(true);
      setBenchmarkCalculationError(undefined);
      setBenchmarkResult(await portfolioReturnApi.calculateBenchmark(
        selectedPeriod[0].format("YYYY-MM-DD"),
        selectedPeriod[1].format("YYYY-MM-DD"),
        selectedIndexes.includes("lqdt"),
        selectedIndexes.includes("indexedBond"),
        selectedIndexes.includes("tmon")
      ));
    } catch (error) {
      setBenchmarkResult(undefined);
      setBenchmarkCalculationError(error instanceof Error ? error.message : "Не удалось рассчитать сравнение с индексами");
    } finally {
      setBenchmarkLoading(false);
    }
  };

  const changeVisibleBenchmarkIndexes = (indexes: string[]) => {
    if (indexes.length === 0) {
      return;
    }

    setVisibleBenchmarkIndexes(indexes);
    void calculateBenchmark(benchmarkPeriod, indexes);
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
          Общая картина портфеля: позиции и фонды ликвидности в рублях.
        </Typography.Paragraph>

        <Space orientation="vertical" size="large" style={{ width: "100%" }}>
          <Tabs
            className="portfolio-main-tabs"
              defaultActiveKey="value"
              destroyOnHidden={false}
              tabBarGutter={16}
              onChange={(activeKey) => {
                window.requestAnimationFrame(() => {
                  const tabs = document.querySelector(".portfolio-main-tabs");
                  const navWrap = tabs?.querySelector(".ant-tabs-nav-wrap");
                  const activeTab = tabs?.querySelector(".ant-tabs-tab-active");
                  if (navWrap instanceof HTMLElement && activeTab instanceof HTMLElement) {
                    const container = navWrap.getBoundingClientRect();
                    const selected = activeTab.getBoundingClientRect();
                    navWrap.scrollLeft += getHorizontalScrollAdjustment(container, selected);
                  }
                });
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
                        <MetricCard title="Общий объём">
                          {summary ? money.format(summary.totalValueRub) : "—"}
                        </MetricCard>
                        <MetricCard title="Позиции">
                          {summary ? money.format(summary.positionsValueRub) : "—"}
                        </MetricCard>
                        <MetricCard title="Фонды">
                          {summary ? money.format(summary.moneyMarketFundsValueRub) : "—"}
                        </MetricCard>
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
                          <div className="portfolio-benchmark-date">
                            <Typography.Text type="secondary">С</Typography.Text>
                            <DatePicker
                              aria-label="Начало периода сравнения"
                              value={benchmarkPeriod[0]}
                              disabledDate={(value) => isBenchmarkStartDateDisabled(value, benchmarkPeriod[1])}
                              onChange={(value) => changeBenchmarkPeriod(value, benchmarkPeriod[1])}
                            />
                          </div>
                          <div className="portfolio-benchmark-date">
                            <Typography.Text type="secondary">По</Typography.Text>
                            <DatePicker
                              aria-label="Конец периода сравнения"
                              value={benchmarkPeriod[1]}
                              disabledDate={(value) => isBenchmarkEndDateDisabled(value, benchmarkPeriod[0])}
                              onChange={(value) => changeBenchmarkPeriod(benchmarkPeriod[0], value)}
                            />
                          </div>
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
                        {benchmarkResult.portfolioUnavailableReason && <Alert
                          type="warning"
                          showIcon
                          title={`Фактический портфель не рассчитан: ${benchmarkResult.portfolioUnavailableReason}`}
                          style={{ marginTop: 12 }}
                        />}
                        {visibleBenchmarkIndexes.includes("indexedBond") && benchmarkResult.indexedBondUnavailableReason && <Alert
                          type="warning"
                          showIcon
                          title={benchmarkResult.indexedBondUnavailableReason}
                          style={{ marginTop: 12 }}
                        />}
                        {visibleBenchmarkIndexes.includes("tmon") && benchmarkResult.tmonUnavailableReason && <Alert
                          type="warning"
                          showIcon
                          title={benchmarkResult.tmonUnavailableReason}
                          style={{ marginTop: 12 }}
                        />}
                        <div className="portfolio-chart-section">
                          {!benchmarkResult.comparisonStartDate && <Alert
                            type="warning"
                            showIcon
                            title="Общую дату для сравнения выбранных инструментов определить нельзя. Снимите недоступные индексы с графика."
                            style={{ marginBottom: 12 }}
                          />}
                          <BenchmarkChart
                            result={benchmarkResult}
                            visibleIndexes={visibleBenchmarkIndexes}
                            onVisibleIndexesChange={changeVisibleBenchmarkIndexes}
                          />
                        </div>
                      </> : !benchmarkCalculationError && <div className="portfolio-chart-empty portfolio-benchmark-empty">
                        Выберите период и нажмите «Сравнить».
                      </div>}
                      </div>
                    </Card>
                  )
                },
                {
                  key: "analytics",
                  label: "Аналитика",
                  children: <PortfolioCurrencyAnalyticsTab />
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
              <Typography.Text type="secondary">После налогов и комиссий</Typography.Text>
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
