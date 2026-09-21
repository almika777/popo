"use client";

import { useEffect, useState } from "react";
import { InfoCircleOutlined } from "@ant-design/icons";
import { App as AntdApp, Card, Descriptions, Divider, Flex, Space, Table, Tag, Tooltip, Typography } from "antd";
import dayjs from "dayjs";
import Link from "next/link";
import type { ColumnsType } from "antd/es/table";
import { portfolioLedgerApi, type PortfolioPosition, type PositionTotals } from "../../lib/portfolio-ledger-api";
import { positionDetailsPath } from "../../lib/position-details";
import { getPositionLiquidityLevel, type PositionLiquidityLevel } from "../../lib/position-liquidity";
import { type PositionRecommendationAction, type PositionRecommendationState } from "../../lib/position-recommendations-api";
import { findPositionRecommendation, formatPositionRecommendationReason } from "../../lib/position-recommendations";

const number = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
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

  const resultType = totals == null ? undefined : totals.unrealizedPnlRub >= 0 ? "success" : "danger";
  const formatResult = (value: number) => `${value >= 0 ? "+" : ""}${number.format(value)}`;

  const columns: ColumnsType<PortfolioPosition> = [
    { title: "Облигация", dataIndex: "shortName", width: 180, render: (value: string, row) => <div><Link href={positionDetailsPath(row.secId, row.boardId)}>{value || row.secId}</Link><div><Typography.Text type="secondary" style={{ whiteSpace: "nowrap" }}>{row.secId} · {row.currencyId}</Typography.Text></div></div> },
    { title: "Количество", dataIndex: "quantity", render: (value: number, row) => {
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
    { title: "Рекомендация", key: "recommendation", render: (_, row) => {
      const recommendation = findPositionRecommendation(recommendationState, row.secId, row.boardId);
      const action = recommendation?.action ?? "Unavailable";
      const presentation = actionPresentation[action];
      const details = recommendation == null
        ? "Рекомендация ещё не рассчитана"
        : <Space orientation="vertical" size={8} style={{ width: "100%" }}>
            <Typography.Text>{recommendation.reasons.map(formatPositionRecommendationReason).join(". ")}</Typography.Text>
            <Descriptions bordered size="small" column={1} items={[
              { key: "current", label: "Текущая", children: `${row.shortName || row.secId} · YTM ${recommendation.ytm == null ? "нет данных" : `${number.format(recommendation.ytm)}%`} · цена расчёта ${recommendation.calculationPrice == null ? "нет данных" : `${number.format(recommendation.calculationPrice)} ${row.currencyId}`} · ${recommendation.rating ?? "без рейтинга"}` },
              { key: "alternative", label: "Альтернатива", children: recommendation.alternative == null ? "Нет сопоставимой бумаги" : `${recommendation.alternative.shortName || recommendation.alternative.secId} (${recommendation.alternative.secId}) · YTM ${number.format(recommendation.alternative.ytm)}% · цена расчёта ${number.format(recommendation.alternative.calculationPrice)} ${row.currencyId} · ${recommendation.alternative.rating} · до ${dayjs(recommendation.alternative.yieldDate).format("DD.MM.YYYY")}` },
              { key: "liquidity", label: "Ликвидность", children: `Средний Volume ${recommendation.averageDailyVolume == null ? "нет данных" : `${integer.format(recommendation.averageDailyVolume)} шт.`}; доля позиции ${recommendation.positionVolumeShare == null ? "нет данных" : `${number.format(recommendation.positionVolumeShare * 100)}%`}` }
            ]} />
          </Space>;
      return <Tooltip
        title={details}
        placement="rightTop"
        styles={{ root: { maxWidth: 560 }, container: { width: 540 } }}>
        <Tag color={presentation.color}>{presentation.label}</Tag>
      </Tooltip>;
    } },
    { title: "Средняя цена", dataIndex: "averageBuyPrice", render: (value: number) => number.format(value) },
    { title: "Цена оценки MOEX", dataIndex: "marketPrice", render: (value: number | null) => value == null ? "Нет данных" : number.format(value) },
    { title: "Стоимость", dataIndex: "marketValue", render: (value: number | null) => value == null ? "Нет данных" : number.format(value) },
    { title: "По чистой цене", dataIndex: "unrealizedPnl", align: "center", render: (value: number | null, row) => value == null
      ? "Нет данных"
      : <Typography.Text type={value >= 0 ? "success" : "danger"} style={{ textAlign: "center" }}>
          <span style={{ display: "block" }}>{formatResult(value)}</span>
          <span style={{ display: "block" }}>({formatResult(row.unrealizedPnlPercent ?? 0)}%)</span>
        </Typography.Text>
    },
    { title: "С купонным доходом", dataIndex: "approximateTotalPnl", align: "center", render: (value: number | null, row) => {
      if (value == null) return "Нет данных";

      const tooltip = row.approximateCouponIncome == null
        ? "Приблизительный купонный доход: нет данных"
        : `Приблизительный купонный доход за срок владения: ${number.format(row.approximateCouponIncome)} ${row.currencyId}. Учтена фактически уплаченная комиссия покупки; НКД сделок не учитывается.`;

      return <Tooltip title={tooltip}>
        <Typography.Text type={value >= 0 ? "success" : "danger"} style={{ textAlign: "center" }}>
          <span style={{ display: "block" }}>{formatResult(value)}</span>
          <span style={{ display: "block" }}>({formatResult(row.approximateTotalPnlPercent ?? 0)}%)</span>
        </Typography.Text>
      </Tooltip>;
    } },
  ];

  return <main className="app-content">
    <Typography.Title level={1}>Текущие позиции</Typography.Title>
    <Typography.Paragraph type="secondary">Позиции рассчитываются из сохранённых сделок и не редактируются напрямую. Нереализованный результат считается по последней доступной цене MOEX.</Typography.Paragraph>
    {recommendationState?.status === "Stale" && <Typography.Paragraph type="warning">Рекомендации устарели. Последний успешный расчёт: {recommendationState.lastSuccessfulAt ? dayjs(recommendationState.lastSuccessfulAt).format("DD.MM.YYYY HH:mm") : "нет данных"}.</Typography.Paragraph>}
    <Card>
      <Flex gap="large" justify="space-between" align="center" wrap>
        <Typography.Text strong>Итого</Typography.Text>
        <Space size={8}><Typography.Text type="secondary">Рыночная стоимость</Typography.Text><Typography.Text strong>{totals == null ? "Нет данных" : `${number.format(totals.marketValueRub)} ₽`}</Typography.Text></Space>
        <Space size={8}><Typography.Text type="secondary">Вложено</Typography.Text><Typography.Text strong>{totals == null ? "Нет данных" : `${number.format(totals.investedAmountRub)} ₽`}</Typography.Text></Space>
        <Space size={8}><Typography.Text type="secondary">Результат</Typography.Text><Typography.Text strong type={resultType}>{totals == null ? "Нет данных" : `${formatResult(totals.unrealizedPnlRub)} ₽ (${formatResult(totals.unrealizedPnlPercent)}%)`}</Typography.Text></Space>
      </Flex>
      <Divider style={{ marginBlock: 12 }} />
      <Table rowKey={(row) => `${row.secId}-${row.boardId}-${row.currencyId}`} loading={loading} columns={columns} dataSource={rows} pagination={{ pageSize: 20 }} />
    </Card>
  </main>;
}
