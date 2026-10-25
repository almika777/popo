"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { App as AntdApp, Button, Card, Col, Form, InputNumber, Popconfirm, Row, Select, Space, Spin, Table, Tooltip, Typography } from "antd";
import { CloseOutlined, DeleteOutlined, EditOutlined } from "@ant-design/icons";
import { Popup } from "antd-mobile";
import type { ColumnsType } from "antd/es/table";
import { portfolioLedgerApi, type MoneyMarketFund, type MoneyMarketFundOption } from "../../lib/portfolio-ledger-api";

type FundForm = { secId: string; quantity: number; averagePrice: number };
const number = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const currentPriceNumber = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 4 });
const integer = new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 0 });

function formatSigned(value: number) {
  return `${value >= 0 ? "+" : ""}${number.format(value)}`;
}

function MobileFundDetailRow({ label, children }: { label: string; children: ReactNode }) {
  return <div className="liquidity-mobile-sheet__row">
    <span className="liquidity-mobile-sheet__label">{label}</span>
    <span className="liquidity-mobile-sheet__value">{children}</span>
  </div>;
}

function MobileFundSheetContent({
  fund,
  onEdit,
  onDelete,
  onClose
}: {
  fund: MoneyMarketFund;
  onEdit: () => void;
  onDelete: () => Promise<void>;
  onClose: () => void;
}) {
  const pnlType = fund.pnl == null ? undefined : fund.pnl >= 0 ? "success" : "danger";

  return <section className="liquidity-mobile-sheet__content" role="dialog" aria-modal="true" aria-labelledby="liquidity-mobile-sheet-title">
    <div className="liquidity-mobile-sheet__handle" aria-hidden="true" />
    <header className="liquidity-mobile-sheet__header">
      <div className="liquidity-mobile-sheet__identity">
        <Typography.Title id="liquidity-mobile-sheet-title" level={4}>{fund.secId}</Typography.Title>
        <Typography.Text type="secondary">Фонд денежного рынка</Typography.Text>
      </div>
      <div className="liquidity-mobile-sheet__actions">
        <Button type="text" aria-label={`Редактировать фонд ${fund.secId}`} icon={<EditOutlined />} onClick={onEdit} />
        <Popconfirm title="Удалить фонд?" okText="Удалить" cancelText="Отмена" placement="bottomRight" onConfirm={onDelete}>
          <Button type="text" danger aria-label={`Удалить фонд ${fund.secId}`} icon={<DeleteOutlined />} />
        </Popconfirm>
        <Button type="text" aria-label="Закрыть состав фонда" icon={<CloseOutlined />} onClick={onClose} />
      </div>
    </header>
    <div className="liquidity-mobile-sheet__rows">
      <MobileFundDetailRow label="Количество">{integer.format(fund.quantity)} шт.</MobileFundDetailRow>
      <MobileFundDetailRow label="Средняя цена">{number.format(fund.averagePrice)} ₽</MobileFundDetailRow>
      <MobileFundDetailRow label="Текущая цена">{fund.currentPrice == null ? "Нет данных" : `${currentPriceNumber.format(fund.currentPrice)} ₽`}</MobileFundDetailRow>
      <MobileFundDetailRow label="Рыночная стоимость">{fund.currentValue == null ? "Нет данных" : `${number.format(fund.currentValue)} ₽`}</MobileFundDetailRow>
      <MobileFundDetailRow label="Результат">
        {fund.pnl == null
          ? "Нет данных"
          : <Typography.Text type={pnlType}>{formatSigned(fund.pnl)} ₽ ({formatSigned(fund.pnlPercent ?? 0)}%)</Typography.Text>}
      </MobileFundDetailRow>
    </div>
  </section>;
}

export default function LiquidityPage() {
  const [fundForm] = Form.useForm<FundForm>();
  const [funds, setFunds] = useState<MoneyMarketFund[]>([]);
  const [fundOptions, setFundOptions] = useState<MoneyMarketFundOption[]>([]);
  const [fundsLoading, setFundsLoading] = useState(true);
  const [editingFundId, setEditingFundId] = useState<string>();
  const [mobileFund, setMobileFund] = useState<MoneyMarketFund | null>(null);
  const [mobileSheetOpen, setMobileSheetOpen] = useState(false);
  const fundEntryRef = useRef<HTMLDivElement>(null);
  const { message } = AntdApp.useApp();

  const loadFunds = useCallback(async () => {
    setFundsLoading(true);
    try {
      const page = await portfolioLedgerApi.getMoneyMarketFundsPage();
      setFunds(page.moneyMarketFunds);
      setFundOptions(page.moneyMarketFundOptions);
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось загрузить фонды");
    } finally {
      setFundsLoading(false);
    }
  }, [message]);

  useEffect(() => {
    void loadFunds();
  }, [loadFunds]);

  const saveFund = async (values: FundForm) => {
    try {
      const updated = editingFundId
        ? await portfolioLedgerApi.updateMoneyMarketFund(editingFundId, values)
        : await portfolioLedgerApi.addMoneyMarketFund(values);
      if (editingFundId) setFunds(current => current.map(row => row.id === editingFundId ? { ...row, ...updated } : row));
      message.success(editingFundId ? "Фонд обновлён" : "Фонд добавлен");
      fundForm.resetFields();
      setEditingFundId(undefined);
      await loadFunds();
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось сохранить фонд");
    }
  };

  const startEditingFund = (fund: MoneyMarketFund, scrollToForm = false) => {
    setEditingFundId(fund.id);
    fundForm.setFieldsValue({ secId: fund.secId, quantity: fund.quantity, averagePrice: fund.averagePrice });
    setMobileSheetOpen(false);
    if (scrollToForm) requestAnimationFrame(() => fundEntryRef.current?.scrollIntoView({ behavior: "smooth", block: "start" }));
  };

  const removeFund = async (fund: MoneyMarketFund) => {
    try {
      await portfolioLedgerApi.deleteMoneyMarketFund(fund.id);
      if (mobileFund?.id === fund.id) setMobileSheetOpen(false);
      await loadFunds();
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось удалить фонд");
    }
  };

  const fundColumns: ColumnsType<MoneyMarketFund> = [
    { title: "Фонд", dataIndex: "secId" },
    { title: "Количество", dataIndex: "quantity" },
    { title: "Средняя", dataIndex: "averagePrice", render: (value: number) => `${number.format(value)} ₽` },
    { title: "Текущая цена", dataIndex: "currentPrice", render: (value: number | null) => value == null ? "Нет данных" : `${number.format(value)} ₽` },
    { title: "Текущая стоимость", dataIndex: "currentValue", render: (value: number | null) => value == null ? "Нет данных" : `${number.format(value)} ₽` },
    { title: <Tooltip title="Текущая стоимость минус себестоимость по указанной средней цене.">Результат</Tooltip>, dataIndex: "pnl", render: (_: number | null, row) => row.pnl == null ? "Нет данных" : `${number.format(row.pnl)} ₽ (${number.format(row.pnlPercent ?? 0)} %)` },
    { title: "Действия", width: 110, render: (_, row) => <Space size="small">
      <Tooltip title="Изменить">
        <Button type="text" size="small" aria-label={`Изменить фонд ${row.secId}`} icon={<EditOutlined />} onClick={() => startEditingFund(row)} />
      </Tooltip>
      <Popconfirm title="Удалить фонд?" okText="Удалить" cancelText="Отмена" onConfirm={() => removeFund(row)}>
        <Tooltip title="Удалить">
          <Button type="text" danger size="small" aria-label={`Удалить фонд ${row.secId}`} icon={<DeleteOutlined />} />
        </Tooltip>
      </Popconfirm>
    </Space> }
  ];

  return <main className="app-content liquidity-page">
    <Typography.Title className="liquidity-page__title" level={1}>Ликвидность</Typography.Title>
    <Typography.Paragraph className="liquidity-page__subtitle" type="secondary">Текущие позиции фондов денежного рынка.</Typography.Paragraph>
    <Card className="liquidity-funds-card" title="Фонды денежного рынка">
      <section className="liquidity-mobile-table" aria-label="Позиции фондов денежного рынка">
        <div className="liquidity-mobile-table__head" aria-hidden="true">
          <span>Фонд / Количество</span>
          <span>Средняя / Текущая цена</span>
          <span>Рыночная стоимость / Результат</span>
        </div>
        {fundsLoading && funds.length === 0 ? <div className="liquidity-mobile-table__state"><Spin /></div> : null}
        {!fundsLoading && funds.length === 0 ? <Typography.Text className="liquidity-mobile-table__empty" type="secondary">Фондов пока нет</Typography.Text> : null}
        {funds.map((fund) => {
          const pnlType = fund.pnl == null ? undefined : fund.pnl >= 0 ? "success" : "danger";
          const isSelected = mobileFund?.id === fund.id;
          return <button
            className={`liquidity-mobile-fund-row${isSelected ? " liquidity-mobile-fund-row--selected" : ""}`}
            key={fund.id}
            type="button"
            aria-haspopup="dialog"
            aria-expanded={isSelected && mobileSheetOpen}
            aria-label={`Открыть состав фонда ${fund.secId}`}
            onClick={() => { setMobileFund(fund); setMobileSheetOpen(true); }}
          >
            <span className="liquidity-mobile-fund-row__identity">
              <strong>{fund.secId}</strong>
              <span>{integer.format(fund.quantity)} шт.</span>
            </span>
            <span className="liquidity-mobile-fund-row__prices">
              <span>Средняя {number.format(fund.averagePrice)} ₽</span>
              <span>Текущая {fund.currentPrice == null ? "Нет данных" : `${currentPriceNumber.format(fund.currentPrice)} ₽`}</span>
            </span>
            <span className="liquidity-mobile-fund-row__value">
              <strong>{fund.currentValue == null ? "Нет данных" : `${number.format(fund.currentValue)} ₽`}</strong>
              <Typography.Text type={pnlType}>{fund.pnl == null ? "Нет данных" : `${formatSigned(fund.pnl)} ₽ (${formatSigned(fund.pnlPercent ?? 0)}%)`}</Typography.Text>
            </span>
          </button>;
        })}
      </section>
      <div className="liquidity-entry-section" ref={fundEntryRef}>
        <Typography.Title className="liquidity-entry-section__title" level={5}>Добавить фонд</Typography.Title>
        <Typography.Paragraph type="secondary">Укажите текущее количество паёв и среднюю цену покупки. Рыночная стоимость и результат рассчитываются по котировкам MOEX.</Typography.Paragraph>
        <Form form={fundForm} layout="vertical" onFinish={saveFund}>
          <Row gutter={[16, 0]}>
            <Col xs={24} sm={12} md={8}><Form.Item name="secId" label="Фонд" rules={[{ required: true, message: "Выберите фонд" }]}><Select placeholder="Выберите фонд" options={fundOptions.map((option) => ({ value: option.secId, label: `${option.secId} (${option.boardId})` }))} /></Form.Item></Col>
            <Col xs={24} sm={12} md={5}><Form.Item name="quantity" label="Количество" rules={[{ required: true, message: "Укажите количество" }]}><InputNumber min={1} precision={0} style={{ width: "100%" }} /></Form.Item></Col>
            <Col xs={24} sm={12} md={5}><Form.Item name="averagePrice" label="Средняя цена" rules={[{ required: true, message: "Укажите среднюю цену" }]}><InputNumber min={0.0001} precision={4} suffix="₽" style={{ width: "100%" }} /></Form.Item></Col>
            <Col xs={24} sm={12} md={6}><Form.Item label=" "><Space><Button type="primary" htmlType="submit">{editingFundId ? "Сохранить изменения" : "Добавить фонд"}</Button>{editingFundId && <Button onClick={() => { setEditingFundId(undefined); fundForm.resetFields(); }}>Отмена</Button>}</Space></Form.Item></Col>
          </Row>
        </Form>
      </div>
      <div className="liquidity-desktop-table">
        <Table rowKey="id" loading={fundsLoading} columns={fundColumns} dataSource={funds} pagination={false} />
      </div>
    </Card>
    <Popup
      visible={mobileSheetOpen}
      position="bottom"
      closeOnSwipe
      closeOnMaskClick
      bodyClassName="liquidity-mobile-sheet"
      bodyStyle={{ bottom: "calc(64px + env(safe-area-inset-bottom))", maxHeight: "calc(100dvh - 64px - env(safe-area-inset-bottom))", overflowY: "auto" }}
      onClose={() => setMobileSheetOpen(false)}
      afterClose={() => setMobileFund(null)}
    >
      {mobileFund == null ? null : <MobileFundSheetContent
        fund={mobileFund}
        onEdit={() => startEditingFund(mobileFund, true)}
        onDelete={() => removeFund(mobileFund)}
        onClose={() => setMobileSheetOpen(false)}
      />}
    </Popup>
  </main>;
}
