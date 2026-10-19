"use client";

import { useCallback, useEffect, useState } from "react";
import dayjs, { type Dayjs } from "dayjs";
import { App as AntdApp, Button, Card, Col, DatePicker, Form, InputNumber, Popconfirm, Row, Select, Space, Table, Tooltip, Typography } from "antd";
import { DeleteOutlined, EditOutlined } from "@ant-design/icons";
import type { ColumnsType } from "antd/es/table";
import { portfolioLedgerApi, type MoneyMarketFund, type MoneyMarketFundOperation, type MoneyMarketFundOption, type TradeSide } from "../../lib/portfolio-ledger-api";

type FundForm = { secId: string; quantity: number; averagePrice: number };
type FundOperationForm = { secId: string; date: Dayjs; side: TradeSide; quantity: number; price: number; commission: number };
const number = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export default function LiquidityPage() {
  const [fundForm] = Form.useForm<FundForm>();
  const [funds, setFunds] = useState<MoneyMarketFund[]>([]);
  const [fundOptions, setFundOptions] = useState<MoneyMarketFundOption[]>([]);
  const [fundOperationForm] = Form.useForm<FundOperationForm>();
  const [fundOperations, setFundOperations] = useState<MoneyMarketFundOperation[]>([]);
  const [fundsLoading, setFundsLoading] = useState(true);
  const [editingFundId, setEditingFundId] = useState<string>();
  const { message } = AntdApp.useApp();

  const loadFunds = useCallback(async () => {
    setFundsLoading(true);
    try {
      const page = await portfolioLedgerApi.getMoneyMarketFundsPage();
      setFunds(page.moneyMarketFunds);
      setFundOptions(page.moneyMarketFundOptions);
      setFundOperations(page.moneyMarketFundOperations);
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось загрузить фонды");
    } finally {
      setFundsLoading(false);
    }
  }, [message]);

  useEffect(() => {
    fundOperationForm.setFieldsValue({ date: dayjs(), side: "Buy", commission: 0.04 });
    void loadFunds();
  }, [fundOperationForm, loadFunds]);

  const saveFundOperation = async (values: FundOperationForm) => {
    try {
      await portfolioLedgerApi.addMoneyMarketFundOperation({ ...values, date: values.date.format("YYYY-MM-DD") });
      message.success(values.side === "Buy" ? "Покупка фонда учтена" : "Продажа фонда учтена");
      fundOperationForm.resetFields();
      fundOperationForm.setFieldsValue({ date: dayjs(), side: "Buy", commission: 0.04 });
      await loadFunds();
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось сохранить операцию фонда");
    }
  };

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

  const fundColumns: ColumnsType<MoneyMarketFund> = [
    { title: "Фонд", dataIndex: "secId" },
    { title: "Количество", dataIndex: "quantity" },
    { title: "Средняя", dataIndex: "averagePrice", render: (value: number) => `${number.format(value)} ₽` },
    { title: "Текущая цена", dataIndex: "currentPrice", render: (value: number | null) => value == null ? "Нет данных" : `${number.format(value)} ₽` },
    { title: "Текущая стоимость", dataIndex: "currentValue", render: (value: number | null) => value == null ? "Нет данных" : `${number.format(value)} ₽` },
    { title: <Tooltip title="Текущая стоимость минус средняя себестоимость текущего остатка. Комиссии покупок включены в себестоимость.">Результат</Tooltip>, dataIndex: "pnl", render: (_: number | null, row) => row.pnl == null ? "Нет данных" : `${number.format(row.pnl)} ₽ (${number.format(row.pnlPercent ?? 0)} %)` },
    { title: "Действия", width: 110, render: (_, row) => <Space size="small">
      <Tooltip title="Изменить">
        <Button type="text" size="small" aria-label={`Изменить фонд ${row.secId}`} icon={<EditOutlined />} onClick={() => { setEditingFundId(row.id); fundForm.setFieldsValue({ secId: row.secId, quantity: row.quantity, averagePrice: row.averagePrice }); }} />
      </Tooltip>
      <Popconfirm title="Удалить фонд?" okText="Удалить" cancelText="Отмена" onConfirm={() => void portfolioLedgerApi.deleteMoneyMarketFund(row.id).then(loadFunds)}>
        <Tooltip title="Удалить">
          <Button type="text" danger size="small" aria-label={`Удалить фонд ${row.secId}`} icon={<DeleteOutlined />} />
        </Tooltip>
      </Popconfirm>
    </Space> }
  ];

  const fundOperationColumns: ColumnsType<MoneyMarketFundOperation> = [
    { title: "Дата", dataIndex: "date" },
    { title: "Фонд", dataIndex: "secId" },
    { title: "Тип", dataIndex: "side", render: (value: TradeSide) => value === "Buy" ? "Покупка" : "Продажа" },
    { title: "Количество", dataIndex: "quantity" },
    { title: "Цена", dataIndex: "price", render: (value: number) => `${number.format(value)} ₽` },
    { title: "Комиссия", dataIndex: "commission", render: (value: number) => `${number.format(value)} ₽` },
    { title: "Сумма", dataIndex: "amount", render: (value: number) => `${number.format(value)} ₽` }
  ];

  return <main className="app-content">
    <Typography.Title level={1}>Ликвидность</Typography.Title>
    <Typography.Paragraph type="secondary">Позиции фондов денежного рынка и операции по ним.</Typography.Paragraph>
    <Card title="Фонды денежного рынка">
      <Typography.Paragraph type="secondary">Ведите количество паёв и среднюю цену покупки. Операции покупки и продажи меняют количество и среднюю себестоимость фонда.</Typography.Paragraph>
      <Form form={fundForm} layout="vertical" onFinish={saveFund}>
        <Row gutter={[16, 0]}>
          <Col xs={24} sm={12} md={8}><Form.Item name="secId" label="Фонд" rules={[{ required: true, message: "Выберите фонд" }]}><Select placeholder="Выберите фонд" options={fundOptions.map((option) => ({ value: option.secId, label: `${option.secId} (${option.boardId})` }))} /></Form.Item></Col>
          <Col xs={24} sm={12} md={5}><Form.Item name="quantity" label="Количество" rules={[{ required: true, message: "Укажите количество" }]}><InputNumber min={1} precision={0} style={{ width: "100%" }} /></Form.Item></Col>
          <Col xs={24} sm={12} md={5}><Form.Item name="averagePrice" label="Средняя цена" rules={[{ required: true, message: "Укажите среднюю цену" }]}><InputNumber min={0.0001} precision={4} suffix="₽" style={{ width: "100%" }} /></Form.Item></Col>
          <Col xs={24} sm={12} md={6}><Form.Item label=" "><Space><Button type="primary" htmlType="submit">{editingFundId ? "Сохранить изменения" : "Добавить фонд"}</Button>{editingFundId && <Button onClick={() => { setEditingFundId(undefined); fundForm.resetFields(); }}>Отмена</Button>}</Space></Form.Item></Col>
        </Row>
      </Form>
      <Table rowKey="id" loading={fundsLoading} columns={fundColumns} dataSource={funds} pagination={false} />
      <Typography.Title level={4} style={{ marginTop: 28 }}>Операции фондов</Typography.Title>
      <Form form={fundOperationForm} layout="vertical" onFinish={saveFundOperation}>
        <Row gutter={[16, 0]}>
          <Col xs={24} sm={12} md={6}><Form.Item name="secId" label="Фонд" rules={[{ required: true, message: "Выберите фонд" }]}><Select placeholder="Выберите фонд" options={fundOptions.map((option) => ({ value: option.secId, label: option.secId }))} /></Form.Item></Col>
          <Col xs={24} sm={12} md={5}><Form.Item name="date" label="Дата" rules={[{ required: true, message: "Укажите дату" }]}><DatePicker style={{ width: "100%" }} /></Form.Item></Col>
          <Col xs={24} sm={12} md={5}><Form.Item name="side" label="Тип операции" rules={[{ required: true, message: "Выберите тип" }]}><Select options={[{ value: "Buy", label: "Покупка" }, { value: "Sell", label: "Продажа" }]} /></Form.Item></Col>
          <Col xs={24} sm={12} md={4}><Form.Item name="quantity" label="Количество" rules={[{ required: true, message: "Укажите количество" }]}><InputNumber min={1} precision={0} style={{ width: "100%" }} /></Form.Item></Col>
          <Col xs={24} sm={12} md={4}><Form.Item name="price" label="Цена" rules={[{ required: true, message: "Укажите цену" }]}><InputNumber min={0.0001} precision={4} suffix="₽" style={{ width: "100%" }} /></Form.Item></Col>
          <Col xs={24} sm={12} md={4}><Form.Item name="commission" label="Комиссия" rules={[{ required: true, message: "Укажите комиссию" }]}><InputNumber min={0} precision={4} suffix="₽" style={{ width: "100%" }} /></Form.Item></Col>
          <Col span={24}><Button type="primary" htmlType="submit">Добавить операцию</Button></Col>
        </Row>
      </Form>
      <Table rowKey="id" loading={fundsLoading} columns={fundOperationColumns} dataSource={fundOperations} pagination={{ pageSize: 10 }} style={{ marginTop: 16 }} />
    </Card>
  </main>;
}
