"use client";

import { useEffect, useMemo, useState } from "react";
import { Alert, App as AntdApp, Button, Card, Col, Collapse, Descriptions, Drawer, Empty, Form, Input, InputNumber, Modal, Row, Select, Space, Table, Tag, Tooltip, Typography } from "antd";
import { InfoCircleOutlined, SettingOutlined } from "@ant-design/icons";
import type { ColumnsType } from "antd/es/table";
import {
  cashInvestmentRecommendationsApi,
  type BondInstrumentType,
  type CashRecommendationBond,
  type CashRecommendationResponse,
  type CreditRating
} from "../../lib/cash-investment-recommendations-api";
import { fromStrategyFormValues, toStrategyFormValues, type StrategyFormValues } from "../../lib/cash-investment-recommendations-form";
import { buildYieldCashFlowRows, recommendationColumnTitles, recommendationYtmActionLabel } from "../../lib/cash-investment-recommendations-table";

const number = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const fullWidth = { width: "100%" } as const;
const calculationDescriptionColumns = { xs: 1, sm: 2 } as const;
type YieldCashFlowRow = ReturnType<typeof buildYieldCashFlowRows>[number];

function formatRating(value: string) {
  return value.replace("_minus", "−").replace("_plus", "+");
}

const ratingOptions: Array<{ value: CreditRating; label: string }> = [
  "AAA", "AAA_minus", "AA_plus", "AA", "AA_minus", "A_plus", "A", "A_minus", "BBB_plus", "BBB", "BBB_minus",
  "BB_plus", "BB", "BB_minus", "B_plus", "B", "B_minus", "CCC", "CC", "C", "D"
].map((value) => ({ value: value as CreditRating, label: formatRating(value) }));

const instrumentTypeOptions: Array<{ value: BondInstrumentType; label: string }> = [
  { value: "Any", label: "Любые" },
  { value: "Floating", label: "Только флоатеры" },
  { value: "Fixed", label: "Только фиксированный купон" }
];

const ratingOrder = ratingOptions.map((option) => option.value);

function formatDate(value: string | null) {
  return value ? value.split("-").reverse().join(".") : "—";
}

function createColumns(onYtmClick: (bond: CashRecommendationBond) => void): ColumnsType<CashRecommendationBond> {
  return [
  {
    title: recommendationColumnTitles[0],
    fixed: "left",
    width: 160,
    render: (_, row) => <Space orientation="vertical" size={0}>
      <Typography.Text strong>{row.secId}</Typography.Text>
      <Typography.Text type="secondary">{row.boardId}</Typography.Text>
    </Space>
  },
  { title: recommendationColumnTitles[1], dataIndex: "issuerName", width: 220 },
  {
    title: recommendationColumnTitles[2],
    dataIndex: "ytm",
    align: "right",
    width: 110,
    sorter: (left, right) => left.ytm - right.ytm,
    render: (value: number, row) => <Tooltip title={recommendationYtmActionLabel}>
      <Button
        type="link"
        size="small"
        aria-label={`Показать расчёт YTM ${row.secId}`}
        onClick={() => onYtmClick(row)}
        style={{ paddingInline: 0, textDecoration: "underline", textUnderlineOffset: 3 }}
      >
        <Space size={4}>
          <Typography.Text type="success" strong>{number.format(value)}%</Typography.Text>
          <InfoCircleOutlined />
        </Space>
      </Button>
    </Tooltip>
  },
  {
    title: recommendationColumnTitles[3],
    dataIndex: "yieldDate",
    width: 150,
    render: (value: string, row) => <Space orientation="vertical" size={2}>
      <Typography.Text>{formatDate(value)}</Typography.Text>
      <Tag color={row.yieldDateType === "Offer" ? "orange" : "blue"}>
        {row.yieldDateType === "Offer" ? "Оферта" : "Погашение"}
      </Tag>
    </Space>
  },
  {
    title: recommendationColumnTitles[4],
    dataIndex: "buyPrice",
    align: "right",
    width: 150,
    render: (value: number, row) => `${number.format(value)} ${row.currencyId}`
  },
  {
    title: recommendationColumnTitles[5],
    dataIndex: "couponType",
    width: 140,
    filters: [{ text: "Фиксированный", value: "Fixed" }, { text: "Флоатер", value: "Floating" }],
    onFilter: (value, row) => row.couponType === value,
    render: (value: CashRecommendationBond["couponType"]) => <Tag color={value === "Floating" ? "orange" : "blue"}>
      {value === "Floating" ? "Флоатер" : "Фиксированный"}
    </Tag>
  },
  { title: recommendationColumnTitles[6], dataIndex: "maturityDate", width: 130, render: formatDate },
  {
    title: recommendationColumnTitles[7],
    dataIndex: "medianDailyVolume",
    align: "right",
    width: 190,
    sorter: (left, right) => left.medianDailyVolume - right.medianDailyVolume,
    render: (value: number) => `${number.format(value)} шт.`
  }
  ];
}

export default function RecommendationsPage() {
  const [form] = Form.useForm<StrategyFormValues>();
  const [result, setResult] = useState<CashRecommendationResponse | null>(null);
  const [presets, setPresets] = useState<Awaited<ReturnType<typeof cashInvestmentRecommendationsApi.getPresets>>>([]);
  const [tradingCurrencies, setTradingCurrencies] = useState<string[]>([]);
  const [faceUnits, setFaceUnits] = useState<string[]>([]);
  const [selectedPresetId, setSelectedPresetId] = useState<number | null>(null);
  const [creatingPreset, setCreatingPreset] = useState(false);
  const [loadingSettings, setLoadingSettings] = useState(true);
  const [recalculating, setRecalculating] = useState(false);
  const [savingSettings, setSavingSettings] = useState(false);
  const [selectedBond, setSelectedBond] = useState<CashRecommendationBond | null>(null);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [mounted, setMounted] = useState(false);
  const [strategyName, setStrategyName] = useState<string>();
  const { message } = AntdApp.useApp();
  const columns = useMemo(() => createColumns(setSelectedBond), []);
  const selectedCashFlows = useMemo(
    () => selectedBond ? buildYieldCashFlowRows(selectedBond.calculation.cashFlows) : [],
    [selectedBond]
  );
  const cashFlowColumns = useMemo<ColumnsType<YieldCashFlowRow>>(() => [
    { title: "Дата", dataIndex: "date", render: formatDate },
    { title: "Тип", dataIndex: "typeLabel" },
    {
      title: "Источник",
      dataIndex: "isProjected",
      render: (isProjected: boolean) => isProjected ? <Tag color="orange">Прогнозный</Tag> : <Tag>Известный</Tag>
    },
    {
      title: "Сумма",
      dataIndex: "amount",
      align: "right",
      render: (amount: number) => `${number.format(amount)} ${selectedBond?.currencyId ?? ""}`
    }
  ], [selectedBond?.currencyId]);

  const applyStrategyFormValues = (values: Partial<StrategyFormValues>) => {
    form.setFieldsValue(values);
    if (values.presetName !== undefined) {
      setStrategyName(values.presetName);
    }
  };

  const ratingGroups = useMemo(() => {
    if (!result) {
      return [];
    }

    const groups = new Map<CreditRating, CashRecommendationBond[]>();
    for (const bond of result.bonds) {
      const bonds = groups.get(bond.rating) ?? [];
      bonds.push(bond);
      groups.set(bond.rating, bonds);
    }

    return [...groups.entries()]
      .sort(([left], [right]) => ratingOrder.indexOf(left) - ratingOrder.indexOf(right))
      .map(([rating, bonds]) => ({
        rating,
        bonds: bonds.toSorted((left, right) => right.ytm - left.ytm),
        minimumYtm: Math.min(...bonds.map((bond) => bond.ytm)),
        maximumYtm: Math.max(...bonds.map((bond) => bond.ytm))
      }));
  }, [result]);

  useEffect(() => {
    setMounted(true);
    const loadSettings = async () => {
      try {
        const page = await cashInvestmentRecommendationsApi.getPage();
        setPresets(page.presets);
        setTradingCurrencies(page.tradingCurrencies);
        setFaceUnits(page.faceUnits);
        const active = page.presets.find(x => x.isActive) ?? page.presets[0];
        if (active) { setSelectedPresetId(active.id); setCreatingPreset(false); applyStrategyFormValues({ ...toStrategyFormValues(active.settings), presetName: active.name }); }
      } catch (error) {
        message.error(error instanceof Error ? error.message : "Не удалось загрузить настройки");
      } finally {
        setLoadingSettings(false);
      }
    };

    void loadSettings();
  }, [form, message]);

  const savePreset = async (values: StrategyFormValues) => {
    setSavingSettings(true);
    try {
      const settings = fromStrategyFormValues(values);
      const saved = creatingPreset
        ? await cashInvestmentRecommendationsApi.createPreset(values.presetName.trim() || "Новый пресет", settings)
        : await cashInvestmentRecommendationsApi.updatePreset(selectedPresetId!, values.presetName.trim() || "Новый пресет", settings);
      await cashInvestmentRecommendationsApi.activatePreset(saved.id);
      setPresets(await cashInvestmentRecommendationsApi.getPresets());
      setSelectedPresetId(saved.id);
      setCreatingPreset(false);
      message.success("Пресет сохранён");
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось сохранить пресет");
    } finally {
      setSavingSettings(false);
    }
  };

  const selectPreset = async (id: number) => {
    const preset = presets.find(x => x.id === id);
    if (!preset) {
      return;
    }

    setSelectedPresetId(id);
    setCreatingPreset(false);
    applyStrategyFormValues({ ...toStrategyFormValues(preset.settings), presetName: preset.name });
    try {
      await cashInvestmentRecommendationsApi.activatePreset(id);
      setResult(null);
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось выбрать пресет");
    }
  };

  const startNewPreset = () => {
    setSelectedPresetId(null);
    setCreatingPreset(true);
    form.resetFields();
    applyStrategyFormValues({ presetName: "Новый пресет" });
    setSettingsOpen(true);
  };

  const deleteSelectedPreset = async () => {
    if (selectedPresetId === null) {
      return;
    }

    try {
      await cashInvestmentRecommendationsApi.deletePreset(selectedPresetId);
      const loaded = await cashInvestmentRecommendationsApi.getPresets();
      setPresets(loaded);
      const active = loaded.find(x => x.isActive) ?? loaded[0];
      if (active) {
        setSelectedPresetId(active.id);
        setCreatingPreset(false);
        applyStrategyFormValues({ ...toStrategyFormValues(active.settings), presetName: active.name });
      }
      setResult(null);
      message.success("Пресет удалён");
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось удалить пресет");
    }
  };

  const recalculate = async () => {
    setRecalculating(true);
    try {
      const response = await cashInvestmentRecommendationsApi.recalculate();
      setResult(response);
      setSettingsOpen(false);
      message.success("Расчёт завершён");
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось рассчитать рекомендации");
    } finally {
      setRecalculating(false);
    }
  };

  const saveSettings = async (values: StrategyFormValues) => {
    setSavingSettings(true);
    try {
      const settings = await cashInvestmentRecommendationsApi.updateSettings(fromStrategyFormValues(values));
      applyStrategyFormValues(toStrategyFormValues(settings));
      setResult(null);
      message.success("Настройки сохранены");
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось сохранить настройки");
    } finally {
      setSavingSettings(false);
    }
  };

  return <main className="app-content recommendations-page">
    <Typography.Title level={1}>Рекомендации по кешу</Typography.Title>
    <Typography.Paragraph type="secondary">
      Расчёт отбирает облигации по рейтингу, сроку, доходности и ликвидности. Цена покупки указана с НКД.
    </Typography.Paragraph>
    <Space orientation="vertical" size="large" style={fullWidth}>
      <div className="recommendations-settings-trigger">
        <div className="recommendations-preset-controls">
          <Typography.Text type="secondary">Пресет</Typography.Text>
          <Select
            aria-label="Пресет стратегии"
            style={{ width: 220 }}
            value={creatingPreset ? undefined : selectedPresetId ?? undefined}
            placeholder="Выберите пресет"
            loading={loadingSettings}
            options={presets.map(x => ({ value: x.id, label: x.name }))}
            onChange={(id: number) => void selectPreset(id)}
          />
          <Button onClick={startNewPreset}>Новый пресет</Button>
          <Button danger disabled={creatingPreset || selectedPresetId === null || presets.length <= 1} onClick={() => void deleteSelectedPreset()}>Удалить</Button>
          <Button type="text" className="recommendations-settings-open" onClick={() => setSettingsOpen(true)}>
            <SettingOutlined />
            <Typography.Text strong>Параметры стратегии</Typography.Text>
            {strategyName && <Typography.Text type="secondary">— {strategyName}</Typography.Text>}
          </Button>
        </div>
        <Button type="primary" size="small" loading={recalculating} onClick={() => void recalculate()}>Рассчитать</Button>
      </div>
      {mounted && <Drawer
        title="Параметры стратегии"
        placement="right"
        size={440}
        open={settingsOpen}
        forceRender
        onClose={() => setSettingsOpen(false)}
        footer={<Button type="primary" onClick={() => form.submit()} loading={savingSettings} block>Сохранить настройки</Button>}
      >
        <Form
          id="recommendation-strategy-form"
          form={form}
          layout="vertical"
          onFinish={savePreset}
          onValuesChange={(changedValues: Partial<StrategyFormValues>) => {
            if (changedValues.presetName !== undefined) {
              setStrategyName(changedValues.presetName);
            }
          }}
        >
          <Row gutter={16}>
            <Col span={24}><Form.Item name="presetName" label="Название пресета" rules={[{ required: true, whitespace: true }]}><Input /></Form.Item></Col>
            <Col span={24}><Form.Item name="minimumRating" label="Минимальный рейтинг" rules={[{ required: true }]}><Select options={ratingOptions} /></Form.Item></Col>
            <Col span={24}>
            <Form.Item label="Срок до погашения, лет">
              <Space.Compact block>
                <Form.Item name="minimumMaturityYears" noStyle>
                  <InputNumber min={0} step={0.5} precision={2} placeholder="от" style={fullWidth} />
                </Form.Item>
                <Form.Item name="maximumMaturityYears" noStyle>
                  <InputNumber min={0} step={0.5} precision={2} placeholder="до" style={fullWidth} />
                </Form.Item>
              </Space.Compact>
            </Form.Item>
          </Col>
            <Col span={24}><Form.Item name="offerWindowDays" label="Исключать оферту ближе, дней" rules={[{ required: true, type: "number", min: 0 }]}><InputNumber min={0} precision={0} style={fullWidth} /></Form.Item></Col>
            <Col span={24}><Form.Item name="instrumentType" label="Тип бумаг" rules={[{ required: true }]}><Select options={instrumentTypeOptions} /></Form.Item></Col>
            <Col span={24}><Form.Item name="faceUnit" label="Валюта номинала"><Select allowClear placeholder="Любая" options={faceUnits.map(value => ({ value, label: value }))} /></Form.Item></Col>
            <Col span={24}><Form.Item name="currencyId" label="Валюта торгов"><Select allowClear placeholder="Любая" options={tradingCurrencies.map(value => ({ value, label: value }))} /></Form.Item></Col>
            <Col span={24}><Form.Item name="minimumMedianDailyVolume" label="Минимальный медианный оборот, шт." rules={[{ required: true, type: "number", min: 1 }]}><InputNumber min={1} precision={0} suffix="шт." style={fullWidth} /></Form.Item></Col>
            <Col span={24}><Form.Item name="minimumYtmPercent" label="Минимальная YTM, %" rules={[{ required: true, type: "number", min: 0.01, max: 100 }]}><InputNumber min={0.01} max={100} step={1} precision={2} suffix="%" style={fullWidth} /></Form.Item></Col>
            <Col span={24}><Form.Item name="maximumYtmPercent" label="Максимальная YTM, %" rules={[{ required: true, type: "number", min: 0.01, max: 100 }]}><InputNumber min={0.01} max={100} step={1} precision={2} suffix="%" style={fullWidth} /></Form.Item></Col>
          </Row>
        </Form>
      </Drawer>}
      <Card
        className="recommendations-result-card"
        title="Результат расчёта"
        extra={result && <Tag color="blue">Снимок: {new Date(result.snapshotTime).toLocaleString("ru-RU")}</Tag>}
      >
        {!result
          ? <Empty description="Нажмите «Рассчитать», чтобы получить список облигаций" />
          : result.bonds.length === 0
            ? <Alert type="info" showIcon title="Подходящих облигаций не найдено" />
            : <Collapse
              key={result.snapshotTime}
              defaultActiveKey={[ratingGroups[0].rating]}
              items={ratingGroups.map((group) => ({
                key: group.rating,
                label: <Space size="large" wrap>
                  <Typography.Title level={4} style={{ margin: 0, minWidth: 52 }}>
                    {formatRating(group.rating)}
                  </Typography.Title>
                  <Tag>{group.bonds.length} {group.bonds.length === 1 ? "облигация" : "облигаций"}</Tag>
                  <Typography.Text>
                    YTM: <Typography.Text type="success" strong>
                      {number.format(group.minimumYtm)}% – {number.format(group.maximumYtm)}%
                    </Typography.Text>
                  </Typography.Text>
                </Space>,
                children: <Table
                  rowKey={(row) => `${row.secId}-${row.boardId}`}
                  columns={columns}
                  dataSource={group.bonds}
                  pagination={false}
                  scroll={{ x: 1250 }}
                />
              }))}
            />}
      </Card>
    </Space>
    <Modal
      title={selectedBond ? `Расчёт YTM: ${selectedBond.secId}` : "Расчёт YTM"}
      open={selectedBond !== null}
      onCancel={() => setSelectedBond(null)}
      footer={null}
      width={800}
      destroyOnHidden
    >
      {selectedBond && <Space orientation="vertical" size="large" style={fullWidth}>
        <Descriptions bordered size="small" column={calculationDescriptionColumns}>
          <Descriptions.Item label="YTM">{number.format(selectedBond.ytm)}%</Descriptions.Item>
          <Descriptions.Item label="Расчёт до">
            {formatDate(selectedBond.yieldDate)} {selectedBond.yieldDateType === "Offer" ? "(оферта)" : "(погашение)"}
          </Descriptions.Item>
          <Descriptions.Item label="Дата расчётов">{formatDate(selectedBond.calculation.settlementDate)}</Descriptions.Item>
          <Descriptions.Item label="Номинал">{number.format(selectedBond.calculation.faceValue)} {selectedBond.currencyId}</Descriptions.Item>
          <Descriptions.Item label="Рыночная цена">{number.format(selectedBond.calculation.marketPricePercent)}%</Descriptions.Item>
          <Descriptions.Item label="Чистая цена">{number.format(selectedBond.calculation.cleanPrice)} {selectedBond.currencyId}</Descriptions.Item>
          <Descriptions.Item label="НКД">{number.format(selectedBond.calculation.accruedInterest)} {selectedBond.currencyId}</Descriptions.Item>
          <Descriptions.Item label="Грязная цена">{number.format(selectedBond.calculation.dirtyPrice)} {selectedBond.currencyId}</Descriptions.Item>
        </Descriptions>
        <Table
          size="small"
          pagination={false}
          rowKey="key"
          dataSource={selectedCashFlows}
          columns={cashFlowColumns}
        />
      </Space>}
    </Modal>
  </main>;
}
