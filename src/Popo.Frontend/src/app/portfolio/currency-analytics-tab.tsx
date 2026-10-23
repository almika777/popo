"use client";

import { useEffect, useRef, useState } from "react";
import {
  Alert,
  Button,
  Card,
  InputNumber,
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
import { buildPortfolioCurrencyAnalyticsPresentation } from "../../lib/portfolio-currency-analytics-presentation";
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

function getAssetKindLabel(asset: PortfolioCurrencyAnalyticsUnvaluedAsset): string {
  if (asset.kind === 1) return "Фонд ликвидности";
  if (asset.kind === 2) return "Денежный остаток";
  return "Позиция";
}

export default function PortfolioCurrencyAnalyticsTab() {
  const nextCashBalanceKey = useRef(0);
  const [cashBalances, setCashBalances] = useState<CashBalanceInput[]>([]);
  const [manualPrices, setManualPrices] = useState<Record<string, number | undefined>>({});
  const [manualRates, setManualRates] = useState<Record<string, number | undefined>>({});
  const [response, setResponse] = useState<PortfolioCurrencyAnalyticsResponse>();
  const [error, setError] = useState<string>();
  const [loading, setLoading] = useState(false);
  const [dirty, setDirty] = useState(false);

  const calculate = async (payload: PortfolioCurrencyAnalyticsRequest) => {
    setLoading(true);
    setError(undefined);
    try {
      setResponse(await portfolioCurrencyAnalyticsApi.calculate(payload));
      setDirty(false);
    } catch (reason) {
      setResponse(undefined);
      setError(reason instanceof Error ? reason.message : "Не удалось рассчитать валютный состав портфеля");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void calculate(emptyPayload);
  }, []);

  const availableCurrencies = response?.availableCurrencies ?? [];
  const presentation = response
    ? buildPortfolioCurrencyAnalyticsPresentation(response)
    : undefined;
  const usedCashCurrencies = new Set(cashBalances.map((balance) => balance.currencyId));
  const missingRateCurrencies = [...new Set((response?.unvaluedAssets ?? [])
    .filter((asset) => asset.missingRate)
    .map((asset) => asset.rateCurrencyId))];

  const updateCashBalance = (key: number, update: Partial<CashBalanceInput>) => {
    setCashBalances((current) => current.map((balance) => balance.key === key ? { ...balance, ...update } : balance));
    setDirty(true);
  };

  const addCashBalance = () => {
    const currencyId = availableCurrencies.find((currency) => !usedCashCurrencies.has(currency)) ?? "RUB";
    setCashBalances((current) => [...current, {
      key: nextCashBalanceKey.current++,
      currencyId,
      amount: undefined
    }]);
    setDirty(true);
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
                          setDirty(true);
                        }}
                      />
                    </div>;
                  })}
                </div>}
          </div>

          {response && missingRateCurrencies.length > 0 && <div className="portfolio-analytics-input-section">
            <Typography.Title level={5}>Курс валюты к рублю</Typography.Title>
            <Typography.Paragraph type="secondary">
              Заполните курс только для валют, по которым нет актуального значения.
            </Typography.Paragraph>
            <div className="portfolio-analytics-rates-list">
              {missingRateCurrencies.map((currencyId) => <label className="portfolio-analytics-rate-row" key={currencyId}>
                <span>{currencyId} → RUB</span>
                <InputNumber
                  aria-label={`Курс ${currencyId} к рублю`}
                  min={0.000001}
                  precision={6}
                  value={manualRates[currencyId]}
                  placeholder="₽ за 1 единицу"
                  onChange={(rateToRub) => {
                    setManualRates((current) => ({ ...current, [currencyId]: rateToRub ?? undefined }));
                    setDirty(true);
                  }}
                />
              </label>)}
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
                      >
                        {presentation.slices.map((slice, index) => <Cell
                          key={slice.currencyId}
                          fill={chartColors[index % chartColors.length]}
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
            {presentation?.slices.length ? <div className="portfolio-analytics-slices">
              {presentation.slices.map((slice, index) => <div className="portfolio-analytics-slice" key={slice.currencyId}>
                <span className="portfolio-analytics-slice-dot" style={{ backgroundColor: chartColors[index % chartColors.length] }} />
                <span className="portfolio-analytics-slice-currency">{slice.currencyId}</span>
                <span className="portfolio-analytics-slice-value">{money.format(slice.valueRub)}</span>
                <span className="portfolio-analytics-slice-share">{percent.format(slice.share)}</span>
              </div>)}
            </div> : presentation?.isEmpty
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
                {asset.missingPrice && <label className="portfolio-analytics-manual-input">
                  <span>Цена за бумагу, {asset.rateCurrencyId}</span>
                  <InputNumber
                    aria-label={`Цена одной бумаги ${asset.name}`}
                    min={0.000001}
                    precision={6}
                    value={manualPrices[asset.assetId]}
                    placeholder="Цена в валюте торгов"
                    onChange={(price) => {
                      setManualPrices((current) => ({ ...current, [asset.assetId]: price ?? undefined }));
                      setDirty(true);
                    }}
                  />
                </label>}
              </div>)}
            </div> : null}
          </div>
        </div>

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
