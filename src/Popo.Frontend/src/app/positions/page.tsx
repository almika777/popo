"use client";

import { useEffect, useState } from "react";
import { DownOutlined, InfoCircleOutlined, RightOutlined } from "@ant-design/icons";
import { App as AntdApp, Card, Select, Space, Spin, Table, Tabs, Tooltip, Typography } from "antd";
import { Popup } from "antd-mobile";
import Link from "next/link";
import type { ColumnsType } from "antd/es/table";
import type { ReactNode } from "react";
import { portfolioLedgerApi, type PortfolioPosition, type PositionTotals } from "../../lib/portfolio-ledger-api";
import { getPositionAverageBuyPrice } from "../../lib/position-average-price";
import { positionDetailsPath } from "../../lib/position-details";
import { getPositionLiquidityLevel, type PositionLiquidityLevel } from "../../lib/position-liquidity";
import ClosedPositionSessionsTab from "./closed-sessions-tab";

const number = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const nominalNumber = new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 4 });
const integer = new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 0 });
const liquidityTextType: Record<PositionLiquidityLevel, "success" | "warning" | "danger"> = {
  low: "success",
  medium: "warning",
  high: "danger"
};

type MobilePositionSortKey = "marketValue" | "result" | "quantity" | "name";

function MobilePositionDetailRow({ label, value, separated = false }: { label: ReactNode; value: ReactNode; separated?: boolean }) {
  return <div className={`position-mobile-detail-row${separated ? " position-mobile-detail-row--separated" : ""}`}>
    <div className="position-mobile-detail-row__main">
      <span className="position-mobile-detail-row__label">{label}</span>
      <span className="position-mobile-detail-row__value">{value}</span>
    </div>
  </div>;
}

function MobilePositionSheetContent({ row }: { row: PortfolioPosition }) {
  const bondName = row.shortName || row.secId;
  const averagePrice = getPositionAverageBuyPrice(row);
  const costBasis = row.marketValue == null || row.unrealizedPnl == null ? null : row.marketValue - row.unrealizedPnl;
  const totalPnlType = row.approximateTotalPnl == null ? undefined : row.approximateTotalPnl >= 0 ? "success" : "danger";
  const formatResult = (value: number) => `${value >= 0 ? "+" : ""}${number.format(value)}`;
  const resultExplanation = "Итого, оценка = переоценка тела + приблизительный купонный результат − комиссия покупки. Не учитываются будущие купоны, налоги и будущая комиссия продажи.";

  return <section className="position-mobile-sheet__content" role="dialog" aria-modal="true" aria-labelledby="position-mobile-sheet-title">
    <div className="position-mobile-sheet__handle" aria-hidden="true" />
    <header className="position-mobile-sheet__header">
      <div className="position-mobile-sheet__identity">
        <Typography.Title id="position-mobile-sheet-title" level={4}>{bondName}</Typography.Title>
        <Typography.Text type="secondary">{row.secId}</Typography.Text>
      </div>
    </header>
    <section className="position-mobile-sheet__section">
      <Typography.Title level={5}>Позиция</Typography.Title>
      <MobilePositionDetailRow label="Количество" value={`${integer.format(row.quantity)} шт.`} />
      <MobilePositionDetailRow
        label="Средняя цена"
        value={averagePrice.value == null ? "Нет данных" : `${number.format(averagePrice.value)} ${row.currencyId}`}
      />
      <MobilePositionDetailRow label="Текущая цена" value={row.marketPrice == null ? "Нет данных" : `${number.format(row.marketPrice)} ${row.currencyId}`} />
      <MobilePositionDetailRow
        label="Стоимость покупки"
        value={costBasis == null ? "Нет данных" : `${number.format(costBasis)} ${row.currencyId}`}
        separated
      />
      <MobilePositionDetailRow label="Рыночная стоимость" value={row.marketValue == null ? "Нет данных" : `${number.format(row.marketValue)} ${row.currencyId}`} />
    </section>
    <section className="position-mobile-sheet__section">
      <Typography.Title level={5}>Результат <Tooltip title={resultExplanation}><InfoCircleOutlined aria-label="Пояснение к результату" /></Tooltip></Typography.Title>
      <MobilePositionDetailRow
        label="Тело, без купонов"
        value={row.unrealizedPnl == null
          ? "Нет данных"
          : <Typography.Text type={row.unrealizedPnl >= 0 ? "success" : "danger"}>
              {formatResult(row.unrealizedPnl)} {row.currencyId}{row.unrealizedPnlPercent == null ? "" : ` (${formatResult(row.unrealizedPnlPercent)}%)`}
            </Typography.Text>}
      />
      <MobilePositionDetailRow label="Купоны, оценка" value={row.approximateCouponIncome == null ? "Нет данных" : `${formatResult(row.approximateCouponIncome)} ${row.currencyId}`} />
      <MobilePositionDetailRow
        label="Итого, оценка"
        value={row.approximateTotalPnl == null
          ? "Нет данных"
          : <Typography.Text strong type={totalPnlType}>
              {formatResult(row.approximateTotalPnl)} {row.currencyId}{row.approximateTotalPnlPercent == null ? "" : ` (${formatResult(row.approximateTotalPnlPercent)}%)`}
            </Typography.Text>}
        separated
      />
      <MobilePositionDetailRow
        label="Годовых, оценка"
        value={row.approximateAnnualizedTotalPnlPercent == null
          ? "Нет данных"
          : <Typography.Text type={row.approximateAnnualizedTotalPnlPercent >= 0 ? "success" : "danger"}>{formatResult(row.approximateAnnualizedTotalPnlPercent)}%</Typography.Text>}
      />
    </section>
    <Link className="position-mobile-sheet__link" href={positionDetailsPath(row.secId, row.boardId)}>
      Подробнее <RightOutlined aria-hidden="true" />
    </Link>
  </section>;
}

export default function PositionsPage() {
  const [rows, setRows] = useState<PortfolioPosition[]>([]);
  const [totals, setTotals] = useState<PositionTotals>();
  const [loading, setLoading] = useState(true);
  const [mobilePosition, setMobilePosition] = useState<PortfolioPosition | null>(null);
  const [mobileSheetOpen, setMobileSheetOpen] = useState(false);
  const [mobileSortKey, setMobileSortKey] = useState<MobilePositionSortKey>("marketValue");
  const [activeTabKey, setActiveTabKey] = useState("open");
  const { message } = AntdApp.useApp();

  useEffect(() => {
    void portfolioLedgerApi.getPositionsPage()
      .then((page) => {
        setRows(page.positions);
        setTotals(page.totals);
      })
      .catch((error) => message.error(error instanceof Error ? error.message : "Не удалось загрузить позиции"))
      .finally(() => setLoading(false));
  }, []);

  const resultType = totals?.unrealizedPnlRub == null ? undefined : totals.unrealizedPnlRub >= 0 ? "success" : "danger";
  const formatResult = (value: number) => `${value >= 0 ? "+" : ""}${number.format(value)}`;
  const mobileRows = [...rows].sort((left, right) => {
    if (mobileSortKey === "name") {
      return (left.shortName || left.secId).localeCompare(right.shortName || right.secId, "ru-RU");
    }

    const getValue = (row: PortfolioPosition) => mobileSortKey === "marketValue"
      ? row.marketValue
      : mobileSortKey === "result"
        ? row.approximateTotalPnl
        : row.quantity;
    const leftValue = getValue(left);
    const rightValue = getValue(right);
    if (leftValue == null) return rightValue == null ? 0 : 1;
    if (rightValue == null) return -1;
    return rightValue - leftValue;
  });

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

    return <div className="position-details-grid">
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
    <div className="positions-page-heading">
      <div>
        <Typography.Title level={1}>Позиции</Typography.Title>
        <Typography.Text className="positions-mobile-subtitle" type="secondary">Облигации Popo°</Typography.Text>
      </div>
      {activeTabKey === "open" && <Select<MobilePositionSortKey>
        className="positions-mobile-sort"
        aria-label="Сортировка позиций"
        value={mobileSortKey}
        suffixIcon={<DownOutlined aria-hidden="true" />}
        onChange={setMobileSortKey}
        options={[
          { value: "marketValue", label: "Рыночная стоимость" },
          { value: "result", label: "Результат" },
          { value: "quantity", label: "Количество" },
          { value: "name", label: "Название" }
        ]}
      />}
    </div>
    <Tabs
      className="portfolio-main-tabs"
      activeKey={activeTabKey}
      onChange={setActiveTabKey}
      items={[
        {
          key: "open",
          label: "Открытые позиции",
          children: <>
            <Typography.Paragraph className="positions-page-description" type="secondary">Средняя цена покупки показывает фактическую чистую цену сделок. Ценовой результат рассчитывается относительно текущего номинала; купонный результат и его годовая экстраполяция остаются приблизительной оценкой.</Typography.Paragraph>
            <Card className="positions-open-card">
              <div className="positions-summary positions-summary-desktop">
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
              <section className="positions-mobile-list" aria-label="Открытые позиции">
                {loading && rows.length === 0 ? <div className="positions-mobile-loading"><Spin /></div> : null}
                {!loading && rows.length === 0 ? <Typography.Text type="secondary">Открытых позиций нет</Typography.Text> : null}
                {mobileRows.map((row) => {
                  const bondName = row.shortName || row.secId;
                  const totalPnlType = row.approximateTotalPnl == null ? undefined : row.approximateTotalPnl >= 0 ? "success" : "danger";
                  const isSelected = mobilePosition?.secId === row.secId
                    && mobilePosition.boardId === row.boardId
                    && mobilePosition.currencyId === row.currencyId;
                  return <article
                    className={`position-mobile-card${isSelected ? " position-mobile-card--selected" : ""}`}
                    key={`${row.secId}-${row.boardId}-${row.currencyId}`}
                    role="button"
                    tabIndex={0}
                    aria-haspopup="dialog"
                    aria-label={`Открыть состав позиции ${bondName}`}
                    onClick={() => {
                      setMobilePosition(row);
                      setMobileSheetOpen(true);
                    }}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" || event.key === " ") {
                        event.preventDefault();
                        setMobilePosition(row);
                        setMobileSheetOpen(true);
                      }
                    }}
                  >
                    <div className="position-mobile-card__header">
                      <div className="position-mobile-card__identity">
                        <span className="position-mobile-card__name">{bondName}</span>
                        <Typography.Text className="position-mobile-card__subtitle" type="secondary">{row.secId}</Typography.Text>
                        <div className="position-mobile-card__summary">
                          <Typography.Text className="position-mobile-card__quantity">{integer.format(row.quantity)} шт.</Typography.Text>
                          <span className="position-mobile-card__separator" aria-hidden="true">•</span>
                          <Typography.Text className="position-mobile-card__percent" type={totalPnlType}>
                            {row.approximateTotalPnlPercent == null ? "Нет данных" : `${formatResult(row.approximateTotalPnlPercent)}%`}
                          </Typography.Text>
                        </div>
                      </div>
                    </div>
                    <div className="position-mobile-card__values">
                      <Typography.Text className="position-mobile-card__value" strong>
                        {row.marketValue == null ? "Нет данных" : `${number.format(row.marketValue)} ${row.currencyId}`}
                      </Typography.Text>
                      <Typography.Text className="position-mobile-card__result" type={totalPnlType}>
                        {row.approximateTotalPnl == null ? "Нет данных" : `${formatResult(row.approximateTotalPnl)} ${row.currencyId}`}
                      </Typography.Text>
                    </div>
                  </article>;
                })}
              </section>
              <Popup
                visible={mobileSheetOpen}
                position="bottom"
                closeOnSwipe
                closeOnMaskClick
                showCloseButton
                bodyClassName="position-mobile-sheet"
                bodyStyle={{ bottom: "calc(64px + env(safe-area-inset-bottom))", maxHeight: "calc(100dvh - 64px - env(safe-area-inset-bottom))", overflowY: "auto" }}
                onClose={() => setMobileSheetOpen(false)}
                afterClose={() => setMobilePosition(null)}
              >
                {mobilePosition == null ? null : <MobilePositionSheetContent row={mobilePosition} />}
              </Popup>
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
