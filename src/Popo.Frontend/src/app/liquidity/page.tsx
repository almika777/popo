"use client";

import { useCallback, useEffect, useState } from "react";
import { App as AntdApp, Button, Card, Col, Form, InputNumber, Popconfirm, Row, Select, Space, Table, Tooltip, Typography } from "antd";
import { DeleteOutlined, EditOutlined } from "@ant-design/icons";
import type { ColumnsType } from "antd/es/table";
import { portfolioLedgerApi, type MoneyMarketFund, type MoneyMarketFundOption } from "../../lib/portfolio-ledger-api";

type FundForm = { secId: string; quantity: number; averagePrice: number };
const number = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export default function LiquidityPage() {
  const [fundForm] = Form.useForm<FundForm>();
  const [funds, setFunds] = useState<MoneyMarketFund[]>([]);
  const [fundOptions, setFundOptions] = useState<MoneyMarketFundOption[]>([]);
  const [fundsLoading, setFundsLoading] = useState(true);
  const [editingFundId, setEditingFundId] = useState<string>();
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

  const fundColumns: ColumnsType<MoneyMarketFund> = [
    { title: "Фонд", dataIndex: "secId" },
    { title: "Количество", dataIndex: "quantity" },
    { title: "Средняя", dataIndex: "averagePrice", render: (value: number) => `${number.format(value)} ₽` },
    { title: "Текущая цена", dataIndex: "currentPrice", render: (value: number | null) => value == null ? "Нет данных" : `${number.format(value)} ₽` },
    { title: "Текущая стоимость", dataIndex: "currentValue", render: (value: number | null) => value == null ? "Нет данных" : `${number.format(value)} ₽` },
    { title: <Tooltip title="Текущая стоимость минус себестоимость по указанной средней цене.">Результат</Tooltip>, dataIndex: "pnl", render: (_: number | null, row) => row.pnl == null ? "Нет данных" : `${number.format(row.pnl)} ₽ (${number.format(row.pnlPercent ?? 0)} %)` },
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

  return <main className="app-content">
    <Typography.Title level={1}>Ликвидность</Typography.Title>
    <Typography.Paragraph type="secondary">Текущие позиции фондов денежного рынка.</Typography.Paragraph>
    <Card title="Фонды денежного рынка">
      <Typography.Paragraph type="secondary">Укажите текущее количество паёв и среднюю цену покупки. Рыночная стоимость и результат рассчитываются по котировкам MOEX.</Typography.Paragraph>
      <Form form={fundForm} layout="vertical" onFinish={saveFund}>
        <Row gutter={[16, 0]}>
          <Col xs={24} sm={12} md={8}><Form.Item name="secId" label="Фонд" rules={[{ required: true, message: "Выберите фонд" }]}><Select placeholder="Выберите фонд" options={fundOptions.map((option) => ({ value: option.secId, label: `${option.secId} (${option.boardId})` }))} /></Form.Item></Col>
          <Col xs={24} sm={12} md={5}><Form.Item name="quantity" label="Количество" rules={[{ required: true, message: "Укажите количество" }]}><InputNumber min={1} precision={0} style={{ width: "100%" }} /></Form.Item></Col>
          <Col xs={24} sm={12} md={5}><Form.Item name="averagePrice" label="Средняя цена" rules={[{ required: true, message: "Укажите среднюю цену" }]}><InputNumber min={0.0001} precision={4} suffix="₽" style={{ width: "100%" }} /></Form.Item></Col>
          <Col xs={24} sm={12} md={6}><Form.Item label=" "><Space><Button type="primary" htmlType="submit">{editingFundId ? "Сохранить изменения" : "Добавить фонд"}</Button>{editingFundId && <Button onClick={() => { setEditingFundId(undefined); fundForm.resetFields(); }}>Отмена</Button>}</Space></Form.Item></Col>
        </Row>
      </Form>
      <Table rowKey="id" loading={fundsLoading} columns={fundColumns} dataSource={funds} pagination={false} />
    </Card>
  </main>;
}
