"use client";

import { useEffect, useState } from "react";
import { DeleteOutlined, EditOutlined } from "@ant-design/icons";
import dayjs, { type Dayjs } from "dayjs";
import { App as AntdApp, Button, Card, Col, DatePicker, Form, Input, InputNumber, Row, Select, Space, Table, Tooltip, Typography } from "antd";
import type { ColumnsType } from "antd/es/table";
import { portfolioLedgerApi, type BondSearchResult, type PortfolioTrade, type TradeSide } from "../../lib/portfolio-ledger-api";

type TradeForm = { secId: string; tradeDate: Dayjs; side: TradeSide; quantity: number; price: number; accruedInterestTotal: number; commission: number };
const number = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const integer = new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 0 });
const bondKey = (bond: BondSearchResult) => `${bond.secId}|${bond.boardId}|${bond.currency}`;

export default function TradesPage() {
  const [form] = Form.useForm<TradeForm>();
  const [rows, setRows] = useState<PortfolioTrade[]>([]);
  const [bonds, setBonds] = useState<BondSearchResult[]>([]);
  const [selectedBond, setSelectedBond] = useState<BondSearchResult>();
  const [editingId, setEditingId] = useState<string>();
  const [loading, setLoading] = useState(true);
  const [pageSize, setPageSize] = useState(20);
  const { message } = AntdApp.useApp();

  const load = async () => { setLoading(true); try { setRows(await portfolioLedgerApi.getTrades()); } catch (error) { message.error(error instanceof Error ? error.message : "Не удалось загрузить сделки"); } finally { setLoading(false); } };
  useEffect(() => {
    form.setFieldsValue({ tradeDate: dayjs() });
    void load();
  }, []);

  const searchBonds = async (value: string) => { if (value.trim().length < 2) return; try { setBonds(await portfolioLedgerApi.searchBonds(value.trim())); } catch (error) { message.error(error instanceof Error ? error.message : "Не удалось выполнить поиск"); } };

  const save = async (values: TradeForm) => {
    if (!selectedBond) { message.error("Выберите облигацию из поиска"); return; }
    try {
      await portfolioLedgerApi.saveTrade({ secId: selectedBond.secId, boardId: selectedBond.boardId, currencyId: selectedBond.currency, tradeDate: values.tradeDate.format("YYYY-MM-DD"), side: values.side, quantity: values.quantity, price: values.price, faceValue: selectedBond.faceValue, accruedInterestTotal: values.accruedInterestTotal, commissionPercent: values.commission ?? 0 }, editingId);
      message.success(editingId ? "Сделка обновлена" : "Сделка сохранена"); form.resetFields(); setSelectedBond(undefined); setEditingId(undefined); await load();
    } catch (error) { message.error(error instanceof Error ? error.message : "Не удалось сохранить сделку"); }
  };

  const columns: ColumnsType<PortfolioTrade> = [
    { title: "Дата", dataIndex: "tradeDate", width: 120, render: (value: string) => <span style={{ whiteSpace: "nowrap" }}>{value}</span> },
    { title: "Облигация", key: "bond", width: 190, render: (_, row) => <div><div>{row.shortName || row.secId}</div><Typography.Text type="secondary" style={{ whiteSpace: "nowrap" }}>{row.secId} · {row.currencyId}</Typography.Text></div> },
    { title: "Тип", dataIndex: "side", render: (value: TradeSide) => value === "Buy" ? "Покупка" : "Продажа" }, { title: "Количество", dataIndex: "quantity", render: (value: number) => integer.format(value) },
    { title: "Цена", dataIndex: "price", render: (value: number) => number.format(value) }, { title: "НКД за 1 шт.", dataIndex: "accruedInterest", render: (value: number) => number.format(value) }, { title: "Комиссия", dataIndex: "commission", render: (value: number) => number.format(value) }, { title: "Сумма", dataIndex: "amount", render: (value: number) => number.format(value) },
    { title: "Действия", width: 100, align: "center", render: (_, row) => <Space size={4}><Tooltip title="Изменить"><Button type="text" size="small" icon={<EditOutlined />} aria-label="Изменить" onClick={() => { const bond = { secId: row.secId, boardId: row.boardId, currency: row.currencyId, shortName: row.shortName, accruedInterest: row.accruedInterest, faceValue: row.faceValue }; setEditingId(row.id); setSelectedBond(bond); setBonds((current) => current.some((item) => bondKey(item) === bondKey(bond)) ? current : [...current, bond]); form.setFieldsValue({ secId: bondKey(bond), tradeDate: dayjs(row.tradeDate), side: row.side, quantity: row.quantity, price: row.price, accruedInterestTotal: row.accruedInterest * row.quantity, commission: row.commissionPercent }); }} /></Tooltip><Tooltip title="Удалить"><Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label="Удалить" onClick={() => void portfolioLedgerApi.deleteTrade(row.id).then(load)} /></Tooltip></Space> }
  ];

  return <main className="app-content">
    <Typography.Title level={1}>Сделки</Typography.Title>
    <Typography.Paragraph type="secondary">Сделка хранится в валюте торгов, выбранной из карточки облигации. Позиции пересчитываются автоматически.</Typography.Paragraph>
    <Space orientation="vertical" size="large" style={{ width: "100%" }}>
      <Card title={editingId ? "Изменить сделку" : "Добавить сделку"} className="trade-editor-card">
        <Form form={form} layout="vertical" onFinish={save} initialValues={{ side: "Buy", accruedInterestTotal: 0, commission: 0.04 }}>
          <Form.Item label="Облигация" name="secId" rules={[{ required: true, message: "Выберите облигацию" }]}>
            <Select showSearch filterOption={false} onSearch={(value) => void searchBonds(value)} onChange={(value) => { const bond = bonds.find((item) => bondKey(item) === value); setSelectedBond(bond); const quantity = form.getFieldValue("quantity"); if (bond && quantity > 0) form.setFieldValue("accruedInterestTotal", bond.accruedInterest * quantity); }} options={bonds.map((bond) => ({ value: bondKey(bond), label: `${bond.shortName || bond.secId} · ${bond.secId} · ${bond.currency} · номинал ${number.format(bond.faceValue)}` }))} placeholder="Поиск облигации" style={{ width: "100%" }} />
          </Form.Item>
          <Row gutter={[16, 0]}>
            <Col xs={24} sm={12} lg={4}><Form.Item label="Дата" name="tradeDate" rules={[{ required: true, message: "Укажите дату" }]}><DatePicker style={{ width: "100%" }} /></Form.Item></Col>
            <Col xs={24} sm={12} lg={4}><Form.Item label="Тип" name="side" rules={[{ required: true, message: "Выберите тип" }]}><Select options={[{ value: "Buy", label: "Покупка" }, { value: "Sell", label: "Продажа" }]} /></Form.Item></Col>
            <Col xs={24} sm={12} lg={4}><Form.Item label="Количество" name="quantity" rules={[{ required: true, message: "Укажите целое количество" }]}><InputNumber min={1} precision={0} style={{ width: "100%" }} placeholder="Шт." /></Form.Item></Col>
            <Col xs={24} sm={12} lg={4}><Form.Item label="Цена" name="price" rules={[{ required: true, message: "Укажите цену" }]}><InputNumber min={0} precision={4} style={{ width: "100%" }} placeholder="За 1 шт." /></Form.Item></Col>
            <Col xs={24} sm={12} lg={4}><Form.Item label="НКД" name="accruedInterestTotal" rules={[{ required: true, message: "Укажите НКД по сделке" }]}><InputNumber min={0} precision={4} style={{ width: "100%" }} placeholder="За все бумаги" /></Form.Item></Col>
            <Col xs={24} sm={12} lg={4}><Form.Item label="Комиссия, %"><Space.Compact block><Form.Item name="commission" noStyle rules={[{ required: true, message: "Укажите комиссию" }]}><InputNumber min={0} precision={4} style={{ width: "calc(100% - 40px)" }} placeholder="0,0000" /></Form.Item><Input value="%" disabled style={{ width: 40, textAlign: "center" }} /></Space.Compact></Form.Item></Col>
            <Col span={24} style={{ display: "flex", justifyContent: "flex-end" }}>
              <Space>{editingId && <Button onClick={() => { setEditingId(undefined); setSelectedBond(undefined); form.resetFields(); }}>Отмена</Button>}<Button type="primary" htmlType="submit">Сохранить</Button></Space>
            </Col>
          </Row>
        </Form>
      </Card>
      <Card title="Журнал сделок"><Table rowKey="id" loading={loading} columns={columns} dataSource={rows} pagination={{ pageSize, showSizeChanger: true, pageSizeOptions: [10, 20, 50, 100], onChange: (_page, size) => setPageSize(size) }} /></Card>
    </Space>
  </main>;
}
