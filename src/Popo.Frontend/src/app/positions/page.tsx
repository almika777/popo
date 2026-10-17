"use client";

import { useEffect, useState } from "react";
import { InfoCircleOutlined } from "@ant-design/icons";
import { App as AntdApp, Card, Descriptions, Space, Table, Tabs, Tag, Tooltip, Typography } from "antd";
import dayjs from "dayjs";
import Link from "next/link";
import type { ColumnsType } from "antd/es/table";
import { portfolioLedgerApi, type PortfolioPosition, type PositionTotals } from "../../lib/portfolio-ledger-api";
import { getPositionAverageBuyPrice } from "../../lib/position-average-price";
import { positionDetailsPath } from "../../lib/position-details";
import { getPositionLiquidityLevel, type PositionLiquidityLevel } from "../../lib/position-liquidity";
import { type PositionRecommendationAction, type PositionRecommendationState } from "../../lib/position-recommendations-api";
import { findPositionRecommendation, formatPositionRecommendationReason } from "../../lib/position-recommendations";
import ClosedPositionSessionsTab from "./closed-sessions-tab";

const number = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const nominalNumber = new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 4 });
const integer = new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 0 });
const liquidityTextType: Record<PositionLiquidityLevel, "success" | "warning" | "danger"> = {
  low: "success",
  medium: "warning",
  high: "danger"
};
const actionPresentation: Record<PositionRecommendationAction, { label: string; color: string }> = {
  Buy: { label: "Купить", color: "green" },
  Hold: { label: "Держать", color: "gold" },
  Sell: { label: "Продать", color: "red" },
  Unavailable: { label: "Нет данных", color: "default" }
};

export default function PositionsPage() {
  const [rows, setRows] = useState<PortfolioPosition[]>([]);
  const [totals, setTotals] = useState<PositionTotals>();
  const [recommendationState, setRecommendationState] = useState<PositionRecommendationState>();
  const [loading, setLoading] = useState(true);
  const { message } = AntdApp.useApp();

  useEffect(() => {
    void portfolioLedgerApi.getPositionsPage()
      .then((page) => {
        setRows(page.positions);
        setTotals(page.totals);
        setRecommendationState(page.recommendation);
      })
      .catch((error) => message.error(error instanceof Error ? error.message : "Не удалось загрузить позиции"))
      .finally(() => setLoading(false));
  }, []);

  const resultType = totals?.unrealizedPnlRub == null ? undefined : totals.unrealizedPnlRub >= 0 ? "success" : "danger";
  const formatResult = (value: number) => `${value >= 0 ? "+" : ""}${number.format(value)}`;

  const columns: ColumnsType<PortfolioPosition> = [
    { title: "Облигация", dataIndex: "shortName", width: 190, render: (value: string, row) => {
      const bondName = value || row.secId;
      return <div className="position-table-identity">
        <Tooltip title={bondName}>
          <Link className="position-bond-name" href={positionDetailsPath(row.secId, row.boardId)}>{bondName}</Link>
        </Tooltip>
        <Typography.Text type="secondary">{row.secId} · {row.currencyId}</Typography.Text>
      </div>;
    } },
    { title: "Кол-во", dataIndex: "quantity", width: 90, render: (value: number, row) => {
      const liquidityLevel = getPositionLiquidityLevel(value, row.averageDailyVolume);
      const tooltip = row.averageDailyVolume == null
        ? "Средний дневной объём: нет данных"
        : `Средний дневной объём: ${integer.format(row.averageDailyVolume)} шт.`;

      return <Tooltip title={tooltip}>
        <Space size={4}>
          <InfoCircleOutlined aria-label={tooltip} />
          <Typography.Text type={liquidityLevel == null ? undefined : liquidityTextType[liquidityLevel]}>{integer.format(value)}</Typography.Text>
        </Space>
      </Tooltip>;
    } },
    { title: "Средняя цена", key: "averageBuyPrice", width: 140, render: (_, row) => {
      const averagePrice = getPositionAverageBuyPrice(row);
      if (averagePrice.value == null) return "Нет данных";

      const adjustedPrice = row.averageBuyPriceAtCurrentFaceValue;
      const tooltip = averagePrice.basis === "indexed-nominal"
        ? "Фактическая средневзвешенная чистая цена покупок. Рост индексируемого номинала учитывается в ценовом результате позиции. Без НКД и комиссии."
        : averagePrice.basis === "current-nominal"
          ? "Оценка средней цены, пересчитанная к текущему номиналу; фактическая средняя цена сделки недоступна."
          : adjustedPrice != null && Math.abs(adjustedPrice - averagePrice.value) > 0.005
            ? `Фактическая средневзвешенная чистая цена покупок. Для расчёта результата используется себестоимость, пересчитанная к текущему номиналу: ${number.format(adjustedPrice)} ${row.currencyId}.`
            : "Фактическая средневзвешенная чистая цена покупок. Без НКД и комиссии.";

      return <Tooltip title={tooltip}>{number.format(averagePrice.value)}</Tooltip>;
    } },
    {
      title: "Рынок",
      key: "marketPrice",
      width: 140,
      render: (_, row) => {
        if (row.marketPrice == null) return "Нет данных";

        const nominal = row.currentFaceValue == null
          ? "номинал неизвестен"
          : `номинал ${nominalNumber.format(row.currentFaceValue)} ${row.faceUnit ?? ""}`;
        const percentOfNominal = row.marketPricePercent == null
          ? "доля номинала неизвестна"
          : `${number.format(row.marketPricePercent)}% от номинала`;

        return <Tooltip title={`Цена MOEX: ${number.format(row.marketPrice)} ${row.currencyId}; ${percentOfNominal}; ${nominal}`}>
          <Typography.Text className="position-result">
            <span>{number.format(row.marketPrice)}</span>
            {row.marketPricePercent == null ? null : <span className="position-result-percent">{number.format(row.marketPricePercent)}% от ном.</span>}
          </Typography.Text>
        </Tooltip>;
      }
    },
    { title: "Стоимость", dataIndex: "marketValue", width: 170, render: (value: number | null) => value == null ? "Нет данных" : number.format(value) },
    {
      title: "Тело",
      dataIndex: "unrealizedPnl",
      width: 145,
      render: (value: number | null, row) => value == null
        ? "Нет данных"
        : <Typography.Text className="position-result" type={value >= 0 ? "success" : "danger"}>
            <span>{formatResult(value)}</span>
            {row.unrealizedPnlPercent == null ? null : <span className="position-result-percent">({formatResult(row.unrealizedPnlPercent)}%)</span>}
          </Typography.Text>
    },
    {
      title: "Итого",
      dataIndex: "approximateTotalPnl",
      width: 165,
      render: (value: number | null, row) => {
        const cleanPnl = row.unrealizedPnl;
        const couponIncome = row.approximateCouponIncome;
        const buyCommission = value == null || cleanPnl == null || couponIncome == null
          ? null
          : Math.round((cleanPnl + Number.EPSILON) * 100) / 100
            + Math.round((couponIncome + Number.EPSILON) * 100) / 100
            - Math.round((value + Number.EPSILON) * 100) / 100;
        const annotation = <div className="position-pnl-breakdown">
          <Typography.Text strong>Состав результата, {row.currencyId}</Typography.Text>
          <Typography.Text>Переоценка тела по чистой цене: {cleanPnl == null ? "нет данных" : formatResult(cleanPnl)}</Typography.Text>
          <Typography.Text>Купонная часть: {couponIncome == null ? "нет данных" : formatResult(couponIncome)}</Typography.Text>
          <Typography.Text>Комиссия покупки открытых лотов: {buyCommission == null ? "нет данных" : `−${number.format(Math.max(0, buyCommission))}`}</Typography.Text>
          <Typography.Text strong>Итого: {value == null ? "нет данных" : formatResult(value)}</Typography.Text>
          <Typography.Text type="secondary">Формула: переоценка тела + купонная часть − комиссия покупки. Тело — текущая стоимость оставшихся бумаг по чистой цене MOEX минус их чистая себестоимость.</Typography.Text>
          <Typography.Text type="secondary">Купонная часть приблизительная: для ЗО и флоатеров учитываются доступная история выплат и НКД; для остальных бумаг — начисление по текущему размеру купона за срок владения.</Typography.Text>
          <Typography.Text type="secondary">Учитывается только открытый остаток. Не учтены налоги, будущие купоны и будущая комиссия продажи.</Typography.Text>
        </div>;

        return <Tooltip title={annotation} placement="top" styles={{ root: { maxWidth: 560 }, container: { width: 520 } }}>
          {value == null
            ? <Typography.Text type="secondary">Нет данных</Typography.Text>
            : <Typography.Text className="position-result" type={value >= 0 ? "success" : "danger"}>
                <span>{formatResult(value)}</span>
                {row.approximateTotalPnlPercent == null ? null : <span className="position-result-percent">({formatResult(row.approximateTotalPnlPercent)}%)</span>}
              </Typography.Text>}
        </Tooltip>;
      }
    },
  ];

  const expandedRowRender = (row: PortfolioPosition) => {
    const annualized = row.approximateAnnualizedTotalPnlPercent;
    const recommendation = findPositionRecommendation(recommendationState, row.secId, row.boardId);
    const action = recommendation?.action ?? "Unavailable";
    const presentation = actionPresentation[action];

    return <div className="position-details-grid">
      <div className="position-detail-metric position-recommendation-metric">
        <Typography.Text type="secondary">Рекомендация</Typography.Text>
        <div className="position-recommendation-details">
          <Tag color={presentation.color}>{presentation.label}</Tag>
          {recommendation == null
            ? <Typography.Text type="secondary">Рекомендация ещё не рассчитана</Typography.Text>
            : <>
                <Typography.Text>{recommendation.reasons.map(formatPositionRecommendationReason).join(". ")}</Typography.Text>
                <Descriptions bordered size="small" column={1} items={[
                  { key: "current", label: "Текущая", children: `${row.shortName || row.secId} · YTM ${recommendation.ytm == null ? "нет данных" : `${number.format(recommendation.ytm)}%`} · цена расчёта ${recommendation.calculationPrice == null ? "нет данных" : `${number.format(recommendation.calculationPrice)} ${row.currencyId}`} · ${recommendation.rating ?? "без рейтинга"}` },
                  { key: "alternative", label: "Альтернатива", children: recommendation.alternative == null ? "Нет сопоставимой бумаги" : `${recommendation.alternative.shortName || recommendation.alternative.secId} (${recommendation.alternative.secId}) · YTM ${number.format(recommendation.alternative.ytm)}% · цена расчёта ${number.format(recommendation.alternative.calculationPrice)} ${row.currencyId} · ${recommendation.alternative.rating} · до ${dayjs(recommendation.alternative.yieldDate).format("DD.MM.YYYY")}` },
                  { key: "liquidity", label: "Ликвидность", children: `Средний Volume ${recommendation.averageDailyVolume == null ? "нет данных" : `${integer.format(recommendation.averageDailyVolume)} шт.`}; доля позиции ${recommendation.positionVolumeShare == null ? "нет данных" : `${number.format(recommendation.positionVolumeShare * 100)}%`}` }
                ]} />
              </>}
        </div>
      </div>
      <div className="position-detail-metric">
        <Typography.Text type="secondary">Годовых, оценка</Typography.Text>
        {annualized == null
          ? <Typography.Text strong>Нет данных</Typography.Text>
          : <Tooltip title="Линейная экстраполяция результата каждой оставшейся покупки на 365 дней с весом по её себестоимости. Для покупки сегодня принимается 1 день владения. Это не прогноз и не YTM; купонный результат приблизительный. Налоги, фактические амортизации и будущая комиссия продажи не учтены.">
              <Typography.Text strong type={annualized >= 0 ? "success" : "danger"}>{formatResult(annualized)}%</Typography.Text>
            </Tooltip>}
        <Typography.Text type="secondary">Линейная оценка · не YTM</Typography.Text>
      </div>
    </div>;
  };

  return <main className="app-content">
    <Typography.Title level={1}>Позиции</Typography.Title>
    <Tabs
      className="portfolio-main-tabs"
      defaultActiveKey="open"
      items={[
        {
          key: "open",
          label: "Открытые позиции",
          children: <>
            <Typography.Paragraph type="secondary">Средняя цена покупки показывает фактическую чистую цену сделок. Ценовой результат рассчитывается относительно текущего номинала; купонный результат и его годовая экстраполяция остаются приблизительной оценкой.</Typography.Paragraph>
            {recommendationState?.status === "Stale" && <Typography.Paragraph type="warning">Рекомендации устарели. Последний успешный расчёт: {recommendationState.lastSuccessfulAt ? dayjs(recommendationState.lastSuccessfulAt).format("DD.MM.YYYY HH:mm") : "нет данных"}.</Typography.Paragraph>}
            <Card>
              <div className="positions-summary">
                <div className="position-summary-item">
                  <Typography.Text type="secondary">Итого</Typography.Text>
                  <Typography.Text className="position-summary-value">{rows.length} позиций</Typography.Text>
                </div>
                <div className="position-summary-item">
                  <Typography.Text type="secondary">Рыночная стоимость</Typography.Text>
                  <Typography.Text className="position-summary-value">{totals == null ? "Нет данных" : `${number.format(totals.marketValueRub)} ₽`}</Typography.Text>
                </div>
                <div className="position-summary-item">
                  <Typography.Text type="secondary">Текущая себестоимость</Typography.Text>
                  <Typography.Text className="position-summary-value">{totals?.investedAmountRub == null ? "Нет данных" : `${number.format(totals.investedAmountRub)} ₽`}</Typography.Text>
                </div>
                <div className="position-summary-item">
                  <Typography.Text type="secondary">Результат</Typography.Text>
                  <Typography.Text className="position-summary-value" type={resultType}>{totals?.unrealizedPnlRub == null || totals.unrealizedPnlPercent == null ? "Нет данных" : `${formatResult(totals.unrealizedPnlRub)} ₽ (${formatResult(totals.unrealizedPnlPercent)}%)`}</Typography.Text>
                </div>
              </div>
              <Table
                className="positions-table"
                rowKey={(row) => `${row.secId}-${row.boardId}-${row.currencyId}`}
                loading={loading}
                columns={columns}
                dataSource={rows}
                tableLayout="fixed"
                scroll={{ x: 1080 }}
                expandable={{ expandedRowRender, columnWidth: 40, rowExpandable: () => true }}
                pagination={{ pageSize: 20 }}
              />
            </Card>
          </>
        },
        {
          key: "closed",
          label: "Закрытые позиции",
          children: <ClosedPositionSessionsTab />
        }
      ]}
    />
  </main>;
}
