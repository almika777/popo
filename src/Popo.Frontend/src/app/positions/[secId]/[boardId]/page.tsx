"use client";

import { use, useEffect, useState } from "react";
import Link from "next/link";
import dayjs, { type Dayjs } from "dayjs";
import { ArrowLeftOutlined } from "@ant-design/icons";
import { App as AntdApp, Button, Card, Col, DatePicker, Form, Input, InputNumber, Row, Select, Space, Table, Typography } from "antd";
import type { ColumnsType } from "antd/es/table";
import { filterPositionTrades } from "../../../../lib/position-details";
import { portfolioLedgerApi, type BondSearchResult, type PortfolioPosition, type PortfolioTrade, type TradeSide } from "../../../../lib/portfolio-ledger-api";

type TradeForm = { tradeDate: Dayjs; side: TradeSide; quantity: number; price: number; accruedInterestTotal: number; commission: number };
const number = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const integer = new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 0 });

export default function PositionDetailsPage({ params }: { params: Promise<{ secId: string; boardId: string }> }) {
  const { secId: encodedSecId, boardId: encodedBoardId } = use(params);
  const secId = decodeURIComponent(encodedSecId);
  const boardId = decodeURIComponent(encodedBoardId);
  const [form] = Form.useForm<TradeForm>();
  const [position, setPosition] = useState<PortfolioPosition>();
  const [trades, setTrades] = useState<PortfolioTrade[]>([]);
  const [instrument, setInstrument] = useState<BondSearchResult>();
  const [editingId, setEditingId] = useState<string>();
  const [loading, setLoading] = useState(true);
  const { message } = AntdApp.useApp();

  const load = async () => {
    setLoading(true);
    try {
      const [positions, allTrades, searchResults] = await Promise.all([
        portfolioLedgerApi.getPositions(),
        portfolioLedgerApi.getTrades(),
        portfolioLedgerApi.searchBonds(secId),
      ]);
      const matchingTrades = filterPositionTrades(allTrades, secId, boardId);
      setPosition(positions.find((item) => item.secId === secId && item.boardId === boardId));
      setTrades(matchingTrades);
      const exactInstrument = searchResults.find((item) => item.secId === secId && item.boardId === boardId);
      const fallbackTrade = matchingTrades[0];
      setInstrument(exactInstrument ?? (fallbackTrade ? { secId, boardId, currency: fallbackTrade.currencyId, shortName: fallbackTrade.shortName || secId, accruedInterest: fallbackTrade.accruedInterest, faceValue: fallbackTrade.faceValue } : undefined));
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось загрузить позицию");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    form.setFieldsValue({ tradeDate: dayjs() });
    void load();
  }, [secId, boardId]);

  const save = async (values: TradeForm) => {
    if (!instrument) { message.error("Не удалось определить параметры инструмента"); return; }
    try {
      const originalTrade = editingId ? trades.find((trade) => trade.id === editingId) : undefined;
      await portfolioLedgerApi.saveTrade({ secId, boardId, currencyId: instrument.currency, tradeDate: values.tradeDate.format("YYYY-MM-DD"), side: values.side, quantity: values.quantity, price: values.price, faceValue: originalTrade?.faceValue ?? instrument.faceValue, accruedInterestTotal: values.accruedInterestTotal, commissionPercent: values.commission ?? 0 }, editingId);
      message.success(editingId ? "Сделка обновлена" : "Сделка сохранена");
      setEditingId(undefined);
      form.resetFields();
      await load();
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось сохранить сделку");
    }
  };

  const edit = (trade: PortfolioTrade) => {
    setEditingId(trade.id);
    form.setFieldsValue({ tradeDate: dayjs(trade.tradeDate), side: trade.side, quantity: trade.quantity, price: trade.price, accruedInterestTotal: trade.accruedInterest * trade.quantity, commission: trade.commissionPercent });
  };

  const columns: ColumnsType<PortfolioTrade> = [
    { title: "Дата", dataIndex: "tradeDate" },
    { title: "Тип", dataIndex: "side", render: (value: TradeSide) => value === "Buy" ? "Покупка" : "Продажа" },
    { title: "Количество", dataIndex: "quantity", render: (value: number) => integer.format(value) },
    { title: "Цена", dataIndex: "price", render: (value: number) => number.format(value) },
    { title: "НКД за 1 шт.", dataIndex: "accruedInterest", render: (value: number) => number.format(value) },
    { title: "Комиссия", dataIndex: "commission", render: (value: number) => number.format(value) },
    { title: "Сумма", dataIndex: "amount", render: (value: number) => number.format(value) },
    { title: "Действия", render: (_, trade) => <Space><Button size="small" onClick={() => edit(trade)}>Изменить</Button><Button size="small" danger onClick={() => void portfolioLedgerApi.deleteTrade(trade.id).then(load)}>Удалить</Button></Space> },
  ];

  return <main className="app-content">
    <Link href="/positions"><ArrowLeftOutlined /> К позициям</Link>
    <Typography.Title level={1} style={{ marginBottom: 4 }}>{position?.shortName || trades[0]?.shortName || secId}</Typography.Title>
    <Typography.Paragraph type="secondary">{secId} · {boardId}{instrument ? ` · ${instrument.currency}` : ""}</Typography.Paragraph>
    <Space orientation="vertical" size="large" style={{ width: "100%" }}>
      <Card title="Текущая позиция" loading={loading}>
        {position ? <Row gutter={[24, 16]}>
          <Col xs={12} md={6}><Typography.Text type="secondary">Количество</Typography.Text><div>{integer.format(position.quantity)}</div></Col>
          <Col xs={12} md={6}><Typography.Text type="secondary">Средняя цена, текущий номинал</Typography.Text><div>{position.averageBuyPriceAtCurrentFaceValue == null ? "Нет данных" : number.format(position.averageBuyPriceAtCurrentFaceValue)}</div>{position.averageBuyPricePercent != null && <Typography.Text type="secondary">{number.format(position.averageBuyPricePercent)}% номинала</Typography.Text>}</Col>
          <Col xs={12} md={6}><Typography.Text type="secondary">Стоимость</Typography.Text><div>{position.marketValue == null ? "Нет данных" : number.format(position.marketValue)}</div></Col>
          <Col xs={12} md={6}><Typography.Text type="secondary">Результат</Typography.Text><div><Typography.Text type={(position.unrealizedPnl ?? 0) >= 0 ? "success" : "danger"}>{position.unrealizedPnl == null ? "Нет данных" : `${position.unrealizedPnl >= 0 ? "+" : ""}${number.format(position.unrealizedPnl)}`}</Typography.Text></div></Col>
          <Col xs={12} md={6}><Typography.Text type="secondary">Текущий номинал</Typography.Text><div>{position.currentFaceValue == null ? "Нет данных" : `${number.format(position.currentFaceValue)} ${position.faceUnit ?? ""}`}</div>{position.marketPricePercent != null && <Typography.Text type="secondary">Котировка {number.format(position.marketPricePercent)}%</Typography.Text>}</Col>
        </Row> : <Typography.Text type="secondary">Открытой позиции нет. Сделки сохранены в журнале ниже.</Typography.Text>}
      </Card>
      <Card title={editingId ? "Изменить сделку" : "Добавить сделку"}>
        <Form form={form} layout="vertical" onFinish={save} initialValues={{ side: "Buy", accruedInterestTotal: 0, commission: 0.04 }}>
          <Row gutter={[16, 0]}>
            <Col xs={24} md={8}><Form.Item label="Дата сделки" name="tradeDate" rules={[{ required: true, message: "Укажите дату" }]}><DatePicker style={{ width: "100%" }} /></Form.Item></Col>
            <Col xs={24} md={8}><Form.Item label="Тип сделки" name="side" rules={[{ required: true }]}><Select options={[{ value: "Buy", label: "Покупка" }, { value: "Sell", label: "Продажа" }]} /></Form.Item></Col>
            <Col xs={24} md={8}><Form.Item label="Количество, шт." name="quantity" rules={[{ required: true }]}><InputNumber min={1} precision={0} style={{ width: "100%" }} /></Form.Item></Col>
            <Col xs={24} md={8}><Form.Item label="Цена за 1 шт." name="price" rules={[{ required: true }]}><InputNumber min={0} precision={4} style={{ width: "100%" }} /></Form.Item></Col>
            <Col xs={24} md={8}><Form.Item label="НКД за все бумаги" name="accruedInterestTotal" rules={[{ required: true }]}><InputNumber min={0} precision={4} style={{ width: "100%" }} /></Form.Item></Col>
            <Col xs={24} md={8}><Form.Item label="Комиссия, %"><Space.Compact block><Form.Item name="commission" noStyle rules={[{ required: true }]}><InputNumber min={0} precision={4} style={{ width: "calc(100% - 40px)" }} /></Form.Item><Input value="%" disabled style={{ width: 40, textAlign: "center" }} /></Space.Compact></Form.Item></Col>
            {instrument && <Col xs={24} md={8}><Typography.Text type="secondary">Номинал: {number.format(instrument.faceValue)} {instrument.currency}</Typography.Text></Col>}
          </Row>
          <Space><Button type="primary" htmlType="submit">{editingId ? "Сохранить изменения" : "Добавить сделку"}</Button>{editingId && <Button onClick={() => { setEditingId(undefined); form.resetFields(); }}>Отмена</Button>}</Space>
        </Form>
      </Card>
      <Card title="Сделки по облигации"><Table rowKey="id" loading={loading} columns={columns} dataSource={trades} pagination={{ pageSize: 20 }} /></Card>
    </Space>
  </main>;
}
