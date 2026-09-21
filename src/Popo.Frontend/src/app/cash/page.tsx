"use client";

import { useEffect, useState } from "react";
import dayjs, { type Dayjs } from "dayjs";
import { App as AntdApp, Button, Card, Col, DatePicker, Form, Input, InputNumber, Popconfirm, Row, Select, Space, Table, Tabs, Tooltip, Typography } from "antd";
import { DeleteOutlined, EditOutlined } from "@ant-design/icons";
import type { ColumnsType } from "antd/es/table";
import { portfolioLedgerApi, type CashBalance, type CashSnapshot, type MoneyMarketFund, type MoneyMarketFundOperation, type MoneyMarketFundOption, type TradeSide } from "../../lib/portfolio-ledger-api";

type CashForm = { currencyId: string; snapshotDate: Dayjs; amount: number; comment?: string };
type FundForm = { secId: string; quantity: number; averagePrice: number };
type FundOperationForm = { secId: string; date: Dayjs; side: TradeSide; quantity: number; price: number; commission: number };
const number = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export default function CashPage() {
  const [form] = Form.useForm<CashForm>();
  const [rows, setRows] = useState<CashSnapshot[]>([]);
  const [balances, setBalances] = useState<CashBalance[]>([]);
  const [currencies, setCurrencies] = useState<string[]>([]);
  const [fundForm] = Form.useForm<FundForm>();
  const [funds, setFunds] = useState<MoneyMarketFund[]>([]);
  const [fundOptions, setFundOptions] = useState<MoneyMarketFundOption[]>([]);
  const [fundOperationForm] = Form.useForm<FundOperationForm>();
  const [fundOperations, setFundOperations] = useState<MoneyMarketFundOperation[]>([]);
  const [editingFundId, setEditingFundId] = useState<string>();
  const [editingId, setEditingId] = useState<string>();
  const [loading, setLoading] = useState(true);
  const { message } = AntdApp.useApp();

  const load = async () => {
    setLoading(true);
    try {
      const page = await portfolioLedgerApi.getCashPage(dayjs().format("YYYY-MM-DD"));
      setRows(page.snapshots); setBalances(page.balances); setCurrencies(page.currencies); setFunds(page.moneyMarketFunds); setFundOptions(page.moneyMarketFundOptions); setFundOperations(page.moneyMarketFundOperations);
    } catch (error) { message.error(error instanceof Error ? error.message : "Не удалось загрузить кеш"); }
    finally { setLoading(false); }
  };
  const saveFundOperation = async (values: FundOperationForm) => {
    try {
      await portfolioLedgerApi.addMoneyMarketFundOperation({ ...values, date: values.date.format("YYYY-MM-DD") });
      message.success(values.side === "Buy" ? "Покупка фонда учтена" : "Продажа фонда учтена");
      fundOperationForm.resetFields(); fundOperationForm.setFieldsValue({ date: dayjs(), side: "Buy", commission: 0 }); await load();
    } catch (error) { message.error(error instanceof Error ? error.message : "Не удалось сохранить операцию фонда"); }
  };

  const saveFund = async (values: FundForm) => {
    try {
      const updated = editingFundId
        ? await portfolioLedgerApi.updateMoneyMarketFund(editingFundId, values)
        : await portfolioLedgerApi.addMoneyMarketFund(values);
      if (editingFundId) setFunds(current => current.map(row => row.id === editingFundId ? { ...row, ...updated } : row));
      message.success(editingFundId ? "Фонд обновлён" : "Фонд добавлен");
      fundForm.resetFields(); setEditingFundId(undefined); await load();
    } catch (error) { message.error(error instanceof Error ? error.message : "Не удалось сохранить фонд"); }
  };
  useEffect(() => {
    form.setFieldsValue({ snapshotDate: dayjs() });
    fundOperationForm.setFieldsValue({ date: dayjs() });
    void load();
  }, []);

  const save = async (values: CashForm) => {
    try {
      const payload = { currencyId: values.currencyId, snapshotDate: values.snapshotDate.format("YYYY-MM-DD"), amount: values.amount, comment: values.comment ?? "" };
      const updated = editingId
        ? await portfolioLedgerApi.updateCashSnapshot(editingId, payload)
        : await portfolioLedgerApi.addCashSnapshot(payload);
      if (editingId) setRows(current => current.map(row => row.id === editingId ? updated : row));
      message.success(editingId ? "Снимок обновлён" : "Снимок сохранён"); form.resetFields(); setEditingId(undefined); await load();
    } catch (error) { message.error(error instanceof Error ? error.message : "Не удалось сохранить снимок"); }
  };

  const columns: ColumnsType<CashSnapshot> = [
    { title: "Дата", dataIndex: "snapshotDate" }, { title: "Валюта", dataIndex: "currencyId" },
    { title: "Остаток", dataIndex: "amount", render: (value: number) => number.format(value) }, { title: "Комментарий", dataIndex: "comment" },
    { title: "Действия", width: 110, render: (_, row) => <Space size="small">
      <Tooltip title="Изменить">
        <Button type="text" size="small" aria-label={`Изменить снимок ${row.snapshotDate}`} icon={<EditOutlined />} onClick={() => { setEditingId(row.id); form.setFieldsValue({ currencyId: row.currencyId, snapshotDate: dayjs(row.snapshotDate), amount: row.amount, comment: row.comment }); }} />
      </Tooltip>
      <Popconfirm title="Удалить снимок?" okText="Удалить" cancelText="Отмена" onConfirm={() => void portfolioLedgerApi.deleteCashSnapshot(row.id).then(load)}>
        <Tooltip title="Удалить">
          <Button type="text" danger size="small" aria-label={`Удалить снимок ${row.snapshotDate}`} icon={<DeleteOutlined />} />
        </Tooltip>
      </Popconfirm>
    </Space> }
  ];

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
      <Popconfirm title="Удалить фонд?" okText="Удалить" cancelText="Отмена" onConfirm={() => void portfolioLedgerApi.deleteMoneyMarketFund(row.id).then(load)}>
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
    <Typography.Paragraph type="secondary">Кеш и фонды денежного рынка учитываются отдельно. Снимки задают фактический остаток, а сделки и операции фондов автоматически пересчитывают баланс.</Typography.Paragraph>
    <Tabs items={[{
      key: "cash",
      label: "Кеш",
      forceRender: true,
      children: <Space orientation="vertical" size="large" style={{ width: "100%" }}>
        <Card title="Текущий расчётный кеш">
          <Space wrap size={[12, 12]}>
            {balances.map((balance) => <Card key={balance.currencyId} size="small" className="cash-balance-card">
              <Typography.Text type="secondary">{balance.currencyId}</Typography.Text>
              <Typography.Title level={4} style={{ margin: "4px 0 0" }}>{number.format(balance.amount)}</Typography.Title>
            </Card>)}
          </Space>
        </Card>
        <Card title="Снимки кеша">
          <Form form={form} layout="vertical" onFinish={save}>
            <Row gutter={[16, 0]}>
              <Col xs={24} sm={8} md={5}><Form.Item name="currencyId" label="Валюта" rules={[{ required: true, message: "Выберите валюту" }]}><Select placeholder="Выберите валюту" options={currencies.map((currency) => ({ value: currency, label: currency }))} /></Form.Item></Col>
              <Col xs={24} sm={8} md={5}><Form.Item name="snapshotDate" label="Дата" rules={[{ required: true, message: "Укажите дату" }]}><DatePicker style={{ width: "100%" }} /></Form.Item></Col>
              <Col xs={24} sm={8} md={5}><Form.Item name="amount" label="Остаток" rules={[{ required: true, message: "Укажите остаток" }]}><InputNumber min={0} precision={4} style={{ width: "100%" }} /></Form.Item></Col>
              <Col xs={24} md={9}><Form.Item name="comment" label="Комментарий"><Input placeholder="Необязательно" /></Form.Item></Col>
              <Col span={24}><Space><Button type="primary" htmlType="submit">{editingId ? "Сохранить изменения" : "Сохранить снимок"}</Button>{editingId && <Button onClick={() => { setEditingId(undefined); form.resetFields(); }}>Отмена</Button>}</Space></Col>
            </Row>
          </Form>
          <Table rowKey="id" loading={loading} columns={columns} dataSource={rows} pagination={{ pageSize: 20 }} style={{ marginTop: 16 }} />
        </Card>
      </Space>
    }, {
      key: "funds",
      label: "Фонды",
      forceRender: true,
      children: <Card title="Фонды денежного рынка">
        <Typography.Paragraph type="secondary">Начальные остатки и операции фондов хранятся отдельно от кеша. Покупки и продажи автоматически меняют количество, среднюю себестоимость фонда и RUB-кеш.</Typography.Paragraph>
        <Form form={fundForm} layout="vertical" onFinish={saveFund}>
          <Row gutter={[16, 0]}>
            <Col xs={24} sm={12} md={8}><Form.Item name="secId" label="Фонд" rules={[{ required: true, message: "Выберите фонд" }]}><Select placeholder="Выберите фонд" options={fundOptions.map((option) => ({ value: option.secId, label: `${option.secId} (${option.boardId})` }))} /></Form.Item></Col>
            <Col xs={24} sm={12} md={5}><Form.Item name="quantity" label="Количество" rules={[{ required: true, message: "Укажите количество" }]}><InputNumber min={1} precision={0} style={{ width: "100%" }} /></Form.Item></Col>
            <Col xs={24} sm={12} md={5}><Form.Item name="averagePrice" label="Средняя цена" rules={[{ required: true, message: "Укажите среднюю цену" }]}><InputNumber min={0.0001} precision={4} suffix="₽" style={{ width: "100%" }} /></Form.Item></Col>
            <Col xs={24} sm={12} md={6}><Form.Item label=" "><Space><Button type="primary" htmlType="submit">{editingFundId ? "Сохранить изменения" : "Добавить фонд"}</Button>{editingFundId && <Button onClick={() => { setEditingFundId(undefined); fundForm.resetFields(); }}>Отмена</Button>}</Space></Form.Item></Col>
          </Row>
        </Form>
        <Table rowKey="id" loading={loading} columns={fundColumns} dataSource={funds} pagination={false} />
        <Typography.Title level={4} style={{ marginTop: 28 }}>Операции фондов</Typography.Title>
        <Form form={fundOperationForm} layout="vertical" onFinish={saveFundOperation} initialValues={{ side: "Buy", commission: 0.04 }}>
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
        <Table rowKey="id" loading={loading} columns={fundOperationColumns} dataSource={fundOperations} pagination={{ pageSize: 10 }} style={{ marginTop: 16 }} />
      </Card>
    }]} />
  </main>;
}
