"use client";

import { useEffect, useRef, useState } from "react";
import {
  Alert,
  Button,
  Card,
  InputNumber,
  Modal,
  Segmented,
  Select,
  Typography
} from "antd";
import { DeleteOutlined, PlusOutlined } from "@ant-design/icons";
import {
  portfolioCurrencyAnalyticsApi,
  type PortfolioCurrencyAnalyticsRequest,
  type PortfolioCurrencyAnalyticsResponse,
  type PortfolioCurrencyAnalyticsUnvaluedAsset
} from "../../lib/portfolio-currency-analytics-api";
import {
  buildPortfolioCurrencyAnalyticsPresentation,
  filterPortfolioCurrencyAnalyticsItems
} from "../../lib/portfolio-currency-analytics-presentation";
import {
  buildManualPriceRows,
  hasAnalyticsInputsChanged,
  getManualRateCurrencies,
  type ManualPriceAsset
} from "../../lib/portfolio-currency-analytics-inputs";
import {
  Cell,
  Pie,
  PieChart,
  ResponsiveContainer
} from "recharts";

type CashBalanceInput = {
  key: number;
  currencyId: string;
  amount?: number;
};

type SegmentItemFilter = "all" | "position" | "cash" | "fund";

const segmentItemFilterOptions: { label: string; value: SegmentItemFilter }[] = [
  { label: "Все активы", value: "all" },
  { label: "Позиции", value: "position" },
  { label: "Деньги", value: "cash" },
  { label: "Фонды", value: "fund" }
];

const segmentCategories = [
  { kind: "Position", label: "Позиции" },
  { kind: "Cash", label: "Денежные средства" },
  { kind: "MoneyMarketFund", label: "Фонды ликвидности" }
] as const;

const segmentItemFilterKinds = {
  position: "Position",
  cash: "Cash",
  fund: "MoneyMarketFund"
} as const;

const money = new Intl.NumberFormat("ru-RU", {
  style: "currency",
  currency: "RUB",
  maximumFractionDigits: 0
});
const percent = new Intl.NumberFormat("ru-RU", {
  style: "percent",
  maximumFractionDigits: 1
});
const chartColors = ["#1677ff", "#13c2c2", "#722ed1", "#fa8c16", "#52c41a", "#eb2f96"];
const emptyPayload: PortfolioCurrencyAnalyticsRequest = {
  cashBalances: [],
  manualPrices: [],
  manualRates: []
};

function getAssetKindLabel(asset: { kind: string }): string {
  if (asset.kind === "MoneyMarketFund") return "Фонд ликвидности";
  if (asset.kind === "Cash") return "Денежные средства";
  return "Позиция";
}

function getMissingValueLabel(asset: { missingPrice: boolean; missingRate: boolean; tradingCurrencyId: string }): string {
  return [asset.missingPrice && "нет цены", asset.missingRate && `нет курса ${asset.tradingCurrencyId}/RUB`]
    .filter(Boolean)
    .join(" · ");
}

export default function PortfolioCurrencyAnalyticsTab() {
  const nextCashBalanceKey = useRef(0);
  const [cashBalances, setCashBalances] = useState<CashBalanceInput[]>([]);
  const [manualPrices, setManualPrices] = useState<Record<string, number | undefined>>({});
  const [manualRates, setManualRates] = useState<Record<string, number | undefined>>({});
  const [manualPriceAssets, setManualPriceAssets] = useState<Record<string, ManualPriceAsset>>({});
  const [response, setResponse] = useState<PortfolioCurrencyAnalyticsResponse>();
  const [selectedCurrencyId, setSelectedCurrencyId] = useState<string>();
  const [segmentModalOpen, setSegmentModalOpen] = useState(false);
  const [segmentItemFilter, setSegmentItemFilter] = useState<SegmentItemFilter>("all");
  const [error, setError] = useState<string>();
  const [loading, setLoading] = useState(false);
  const [dirty, setDirty] = useState(false);
  const inputRevision = useRef(0);

  const markDirty = () => {
    inputRevision.current += 1;
    setDirty(true);
  };

  const calculate = async (payload: PortfolioCurrencyAnalyticsRequest) => {
    const requestRevision = inputRevision.current;
    setLoading(true);
    setError(undefined);
    try {
      setResponse(await portfolioCurrencyAnalyticsApi.calculate(payload));
      setDirty(hasAnalyticsInputsChanged(requestRevision, inputRevision.current));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Не удалось рассчитать валютный состав портфеля");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void calculate(emptyPayload);
  }, []);

  const availableCurrencies = response?.availableCurrencies ?? [];
  const unvaluedAssets = response?.unvaluedAssets ?? [];
  const presentation = response
    ? buildPortfolioCurrencyAnalyticsPresentation(response)
    : undefined;
  const selectedGroup = presentation?.groups.find((group) => group.currencyId === selectedCurrencyId)
    ?? presentation?.groups[0];
  const openSegmentDetails = (currencyId: string) => {
    setSelectedCurrencyId(currencyId);
    setSegmentItemFilter("all");
    setSegmentModalOpen(true);
  };
  const detailItems = selectedGroup
    ? filterPortfolioCurrencyAnalyticsItems(selectedGroup.items, segmentItemFilter)
    : [];
  const filteredCategoryKind = segmentItemFilter === "all"
    ? undefined
    : segmentItemFilterKinds[segmentItemFilter];
  const getCurrencyColor = (currencyId: string) => {
    const groupIndex = presentation?.groups.findIndex((group) => group.currencyId === currencyId) ?? 0;
    return chartColors[Math.max(0, groupIndex) % chartColors.length];
  };
  const usedCashCurrencies = new Set(cashBalances.map((balance) => balance.currencyId));
  const manualPriceRows = buildManualPriceRows(unvaluedAssets, Object.values(manualPriceAssets));
  const manualRateCurrencies = getManualRateCurrencies(unvaluedAssets, manualRates);

  const updateManualPrice = (asset: ManualPriceAsset, price?: number) => {
    setManualPriceAssets((current) => ({ ...current, [asset.assetId]: asset }));
    setManualPrices((current) => ({ ...current, [asset.assetId]: price }));
    markDirty();
  };

  const updateCashBalance = (key: number, update: Partial<CashBalanceInput>) => {
    setCashBalances((current) => current.map((balance) => balance.key === key ? { ...balance, ...update } : balance));
    markDirty();
  };

  const addCashBalance = () => {
    const currencyId = availableCurrencies.find((currency) => !usedCashCurrencies.has(currency)) ?? "RUB";
    setCashBalances((current) => [...current, {
      key: nextCashBalanceKey.current++,
      currencyId,
      amount: undefined
    }]);
    markDirty();
  };

  const calculateInputs = () => {
    const payload: PortfolioCurrencyAnalyticsRequest = {
      cashBalances: cashBalances
        .filter((balance) => balance.currencyId.trim().length > 0 && balance.amount !== undefined)
        .map(({ currencyId, amount }) => ({ currencyId, amount: amount! })),
      manualPrices: Object.entries(manualPrices)
        .filter((entry): entry is [string, number] => entry[1] !== undefined && Number.isFinite(entry[1]) && entry[1] > 0)
        .map(([assetId, price]) => ({ assetId, price })),
      manualRates: Object.entries(manualRates)
        .filter((entry): entry is [string, number] => entry[1] !== undefined && Number.isFinite(entry[1]) && entry[1] > 0)
        .map(([currencyId, rateToRub]) => ({ currencyId, rateToRub }))
    };

    void calculate(payload);
  };

  return (
    <Card className="portfolio-overview-card portfolio-analytics-card">
      <div className="portfolio-analytics-content">
        <div className="portfolio-analytics-heading">
          <div>
            <Typography.Title level={3}>Состав портфеля</Typography.Title>
            <Typography.Text type="secondary">
              Доли по валюте номинала; оценка в рублях по валюте торгов
            </Typography.Text>
          </div>
          {response && <Typography.Text type="secondary">На {response.asOf}</Typography.Text>}
        </div>

        <section className="portfolio-analytics-inputs" aria-label="Денежные остатки и ручные оценки">
          <div className="portfolio-analytics-input-section">
            <div className="portfolio-analytics-section-heading">
              <Typography.Title level={5}>Деньги в портфеле</Typography.Title>
              <Button
                icon={<PlusOutlined />}
                size="small"
                onClick={addCashBalance}
                disabled={availableCurrencies.length === 0 || usedCashCurrencies.size >= availableCurrencies.length}
              >
                Добавить валюту
              </Button>
            </div>
            {cashBalances.length === 0
              ? <Typography.Text type="secondary">Добавьте остатки денег, если они есть у брокера.</Typography.Text>
              : <div className="portfolio-analytics-cash-list">
                  {cashBalances.map((balance) => {
                    const currencyOptions = availableCurrencies
                      .filter((currency) => currency === balance.currencyId || !usedCashCurrencies.has(currency))
                      .map((currency) => ({ value: currency, label: currency }));
                    return <div className="portfolio-analytics-cash-row" key={balance.key}>
                      <Select
                        aria-label="Валюта остатка"
                        value={balance.currencyId}
                        options={currencyOptions}
                        onChange={(currencyId) => updateCashBalance(balance.key, { currencyId })}
                      />
                      <InputNumber
                        aria-label={`Остаток в ${balance.currencyId}`}
                        min={0}
                        precision={2}
                        value={balance.amount}
                        placeholder="Сумма"
                        onChange={(amount) => updateCashBalance(balance.key, { amount: amount ?? undefined })}
                      />
                      <Button
                        aria-label={`Удалить остаток ${balance.currencyId}`}
                        danger
                        type="text"
                        icon={<DeleteOutlined />}
                        onClick={() => {
                          setCashBalances((current) => current.filter((item) => item.key !== balance.key));
                          markDirty();
                        }}
                      />
                    </div>;
                  })}
                </div>}
          </div>

          {response && manualRateCurrencies.length > 0 && <div className="portfolio-analytics-input-section">
            <Typography.Title level={5}>Курс валюты к рублю</Typography.Title>
            <Typography.Paragraph type="secondary">
              Курсы временные: можно изменить или сбросить их и пересчитать портфель.
            </Typography.Paragraph>
            <div className="portfolio-analytics-rates-list">
              {manualRateCurrencies.map((currencyId) => <div className="portfolio-analytics-rate-row" key={currencyId}>
                <span>{currencyId} → RUB</span>
                <div className="portfolio-analytics-input-with-reset">
                  <InputNumber
                    aria-label={`Курс ${currencyId} к рублю`}
                    min={0.000001}
                    precision={6}
                    value={manualRates[currencyId]}
                    placeholder="₽ за 1 единицу"
                    onChange={(rateToRub) => {
                      setManualRates((current) => ({ ...current, [currencyId]: rateToRub ?? undefined }));
                      markDirty();
                    }}
                  />
                  <Button
                    aria-label={`Сбросить ручной курс ${currencyId}`}
                    danger
                    type="text"
                    icon={<DeleteOutlined />}
                    onClick={() => {
                      setManualRates((current) => ({ ...current, [currencyId]: undefined }));
                      markDirty();
                    }}
                  />
                </div>
              </div>)}
            </div>
          </div>}

          {response && manualPriceRows.length > 0 && <div className="portfolio-analytics-input-section">
            <Typography.Title level={5}>Цена активов</Typography.Title>
            <Typography.Paragraph type="secondary">
              Укажите цену за одну бумагу или пай в валюте торгов. Ручные значения остаются доступны для изменения.
            </Typography.Paragraph>
            <div className="portfolio-analytics-unvalued">
              {manualPriceRows.map((asset) => <div className="portfolio-analytics-unvalued-item" key={asset.assetId}>
                <div className="portfolio-analytics-unvalued-description">
                  <Typography.Text strong>{asset.name}</Typography.Text>
                  <Typography.Text type="secondary">{getAssetKindLabel(asset)} · группа {asset.currencyId}</Typography.Text>
                </div>
                <div className="portfolio-analytics-manual-input">
                  <span>Цена за бумагу, {asset.rateCurrencyId}</span>
                  <div className="portfolio-analytics-input-with-reset">
                    <InputNumber
                      aria-label={`Цена одной бумаги ${asset.name}`}
                      min={0.000001}
                      precision={6}
                      value={manualPrices[asset.assetId]}
                      placeholder="Цена в валюте торгов"
                      onChange={(price) => updateManualPrice(asset, price ?? undefined)}
                    />
                    <Button
                      aria-label={`Сбросить ручную цену ${asset.name}`}
                      danger
                      type="text"
                      icon={<DeleteOutlined />}
                      onClick={() => updateManualPrice(asset, undefined)}
                    />
                  </div>
                </div>
              </div>)}
            </div>
          </div>}
        </section>

        {dirty && <Alert
          className="portfolio-analytics-dirty-alert"
          type="info"
          showIcon
          title="Ввод изменён — пересчитайте портфель, чтобы обновить диаграмму."
        />}
        {error && <Alert
          className="portfolio-analytics-error"
          type="error"
          showIcon
          title={error}
        />}

        <div className="portfolio-analytics-results">
          <div className={`portfolio-analytics-chart-panel${presentation?.slices.length ? "" : " portfolio-analytics-chart-panel-empty"}`}>
            <div className="portfolio-analytics-total">
              <Typography.Text type="secondary">Оценённая часть</Typography.Text>
              <Typography.Title level={2}>
                {presentation ? money.format(presentation.totalValueRub) : "—"}
              </Typography.Title>
              {presentation?.isPartial && <Typography.Text type="warning">Неполная оценка</Typography.Text>}
            </div>
            {presentation && presentation.slices.length > 0
              ? <div className="portfolio-analytics-donut" role="img" aria-label="Доли портфеля по валютам">
                  <ResponsiveContainer width="100%" height="100%">
                    <PieChart>
                      <Pie
                        data={presentation.slices}
                        dataKey="valueRub"
                        nameKey="currencyId"
                        innerRadius="68%"
                        outerRadius="90%"
                        paddingAngle={2}
                        stroke="none"
                        isAnimationActive={false}
                        onClick={(_, index) => {
                          const currencyId = presentation.slices[index]?.currencyId;
                          if (currencyId) openSegmentDetails(currencyId);
                        }}
                      >
                        {presentation.slices.map((slice) => <Cell
                          key={slice.currencyId}
                          fill={getCurrencyColor(slice.currencyId)}
                        />)}
                      </Pie>
                    </PieChart>
                  </ResponsiveContainer>
                </div>
              : <div className="portfolio-analytics-chart-empty">
                  {loading
                    ? "Рассчитываем портфель…"
                    : error
                      ? "Данные для диаграммы недоступны"
                      : presentation?.isEmpty
                        ? "Портфель пока пуст"
                        : "Нет оценённых активов"}
                </div>}
          </div>

          <div className="portfolio-analytics-breakdown">
            {presentation?.isPartial && <Alert
              type="warning"
              showIcon
              title={`Не оценено активов: ${presentation.unvaluedAssets.length}. Они не входят в сумму и доли.`}
            />}
            {presentation?.groups.length ? <>
              <Typography.Text className="portfolio-analytics-breakdown-hint" type="secondary">
                Нажмите на валюту, чтобы открыть состав сегмента.
              </Typography.Text>
              <div className="portfolio-analytics-slices" aria-label="Валютные сегменты">
              {presentation.groups.map((group) => <button
                className={`portfolio-analytics-slice${selectedCurrencyId === group.currencyId ? " portfolio-analytics-slice-selected" : ""}`}
                key={group.currencyId}
                type="button"
                aria-pressed={selectedCurrencyId === group.currencyId}
                onClick={() => openSegmentDetails(group.currencyId)}
              >
                <span className="portfolio-analytics-slice-dot" style={{ backgroundColor: getCurrencyColor(group.currencyId) }} />
                <span className="portfolio-analytics-slice-currency">{group.currencyId}</span>
                <span className="portfolio-analytics-slice-value">
                  {group.valueRub > 0 ? money.format(group.valueRub) : "Не оценено"}
                </span>
                <span className="portfolio-analytics-slice-share">
                  {group.valueRub > 0 ? percent.format(group.share) : "—"}
                </span>
              </button>)}
              </div>
            </> : presentation?.isEmpty
              ? <div className="portfolio-chart-empty portfolio-analytics-empty">Добавьте активы и денежные остатки — здесь появится распределение.</div>
              : null}

            {presentation?.unvaluedAssets.length ? <div className="portfolio-analytics-unvalued">
              <Typography.Title level={5}>Не удалось оценить</Typography.Title>
              {presentation.unvaluedAssets.map((asset) => <div className="portfolio-analytics-unvalued-item" key={`${asset.kind}:${asset.assetId}`}>
                <div className="portfolio-analytics-unvalued-description">
                  <Typography.Text strong>{asset.name}</Typography.Text>
                  <Typography.Text type="secondary">{getAssetKindLabel(asset)} · группа {asset.currencyId}</Typography.Text>
                  <Typography.Text type="warning">
                    {[asset.missingPrice && "нет цены", asset.missingRate && `нет курса ${asset.rateCurrencyId}/RUB`]
                      .filter(Boolean)
                      .join(" · ")}
                  </Typography.Text>
                </div>
              </div>)}
            </div> : null}
          </div>
        </div>

        <Modal
          className="portfolio-analytics-segment-modal"
          title={selectedGroup ? `Состав сегмента ${selectedGroup.currencyId}` : "Состав сегмента"}
          open={segmentModalOpen && Boolean(selectedGroup)}
          onCancel={() => setSegmentModalOpen(false)}
          footer={null}
          width={1040}
          destroyOnHidden
        >
          {selectedGroup && <section className="portfolio-analytics-segment-details" aria-label={`Состав сегмента ${selectedGroup.currencyId}`}>
          <div className="portfolio-analytics-detail-main">
            <div className="portfolio-analytics-detail-heading">
              <Typography.Text type="secondary">Группа определяется валютой номинала; денежные суммы показаны в рублях.</Typography.Text>
            </div>
            <Segmented
              aria-label="Фильтр состава сегмента"
              options={segmentItemFilterOptions}
              value={segmentItemFilter}
              onChange={(value) => setSegmentItemFilter(value as SegmentItemFilter)}
            />
            <div className="portfolio-analytics-detail-sections">
              {segmentCategories.map((category) => {
                const categoryItems = detailItems.filter((item) => item.kind === category.kind);
                if (filteredCategoryKind && category.kind !== filteredCategoryKind) return null;
                if (segmentItemFilter === "all" && categoryItems.length === 0) return null;
                const categoryValueRub = categoryItems.reduce(
                  (total, item) => total + (item.valueRub ?? 0),
                  0
                );
                const unvaluedCount = categoryItems.filter((item) => item.valueRub === null).length;

                return <section className="portfolio-analytics-detail-section" key={category.kind}>
                  <div className="portfolio-analytics-detail-section-heading">
                    <Typography.Title level={5}>{category.label}</Typography.Title>
                    <Typography.Text>
                      {categoryValueRub > 0 ? money.format(categoryValueRub) : categoryItems.length ? "Не оценено" : "0 ₽"}
                    </Typography.Text>
                  </div>
                  {unvaluedCount > 0 && <Typography.Text type="warning">Не оценено активов: {unvaluedCount}</Typography.Text>}
                  {categoryItems.length > 0 ? categoryItems.map((item) => <div
                    className="portfolio-analytics-detail-item"
                    key={`${item.kind}:${item.assetId}`}
                  >
                    <span className="portfolio-analytics-slice-dot" style={{ backgroundColor: getCurrencyColor(item.currencyId) }} />
                    <div className="portfolio-analytics-detail-description">
                      <Typography.Text strong>{item.name}</Typography.Text>
                      <Typography.Text type="secondary">{item.tradingCurrencyId} · {getAssetKindLabel(item)}</Typography.Text>
                      {item.valueRub === null && <Typography.Text type="warning">Не включено в сумму: {getMissingValueLabel(item)}</Typography.Text>}
                    </div>
                    <Typography.Text className="portfolio-analytics-detail-item-value">
                      {item.valueRub === null ? "Не оценено" : money.format(item.valueRub)}
                    </Typography.Text>
                  </div>) : <Typography.Text type="secondary">В этом сегменте нет активов этой категории.</Typography.Text>}
                </section>;
              })}
              {detailItems.length === 0 && <Typography.Text type="secondary">В этом сегменте пока нет активов.</Typography.Text>}
            </div>
          </div>

          <aside className="portfolio-analytics-segment-summary">
            <Typography.Title level={4}>Сегмент {selectedGroup.currencyId}</Typography.Title>
            <Typography.Title level={2} className="portfolio-analytics-segment-total">
              {selectedGroup.valueRub > 0 ? money.format(selectedGroup.valueRub) : "Не оценено"}
            </Typography.Title>
            <Typography.Text type="secondary">
              {selectedGroup.valueRub > 0 ? `${percent.format(selectedGroup.share)} портфеля` : "Нет оценённых активов"}
            </Typography.Text>
            <div className="portfolio-analytics-segment-metrics">
              <div className="portfolio-analytics-segment-metric portfolio-analytics-segment-metric-total">
                <Typography.Text>Количество активов</Typography.Text>
                <Typography.Text strong>{selectedGroup.items.length}</Typography.Text>
              </div>
              {segmentCategories.map((category) => <div className="portfolio-analytics-segment-metric" key={category.kind}>
                <Typography.Text type="secondary">{category.label}</Typography.Text>
                <Typography.Text>{selectedGroup.items.filter((item) => item.kind === category.kind).length}</Typography.Text>
              </div>)}
            </div>
          </aside>
          </section>}
        </Modal>

        <div className="portfolio-analytics-actions">
          <Typography.Text type="secondary">
            Остатки денег, цены и курсы используются только для этого расчёта и не сохраняются.
          </Typography.Text>
          <Button type="primary" loading={loading} onClick={calculateInputs}>
            Пересчитать
          </Button>
        </div>
      </div>
    </Card>
  );
}
