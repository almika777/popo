"use client";

import { useEffect, useState } from "react";
import { CloseOutlined, DeleteOutlined, EditOutlined } from "@ant-design/icons";
import dayjs, { type Dayjs } from "dayjs";
import { App as AntdApp, Button, Card, Col, DatePicker, Form, Input, InputNumber, Pagination, Popconfirm, Row, Select, Space, Spin, Table, Tooltip, Typography } from "antd";
import { Popup } from "antd-mobile";
import type { ColumnsType } from "antd/es/table";
import type { ReactNode } from "react";
import { MIN_BOND_SEARCH_LENGTH, portfolioLedgerApi, type BondSearchResult, type PortfolioTrade, type TradeSide } from "../../lib/portfolio-ledger-api";
import { calculateAccruedInterestTotal } from "../../lib/trade-form";

type TradeForm = { secId: string; tradeDate: Dayjs; side: TradeSide; quantity: number; price: number; accruedInterestTotal?: number; commission: number };
const number = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const amountNumber = new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 2 });
const integer = new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 0 });
const tradePageSizeOptions = [10, 20, 50, 100];
const bondKey = (bond: BondSearchResult) => `${bond.secId}|${bond.boardId}|${bond.currency}`;

function formatTradeMoney(value: number, currencyId: string) {
  return `${amountNumber.format(value)} ${currencyId === "RUB" ? "₽" : currencyId}`;
}

function MobileTradeDetailRow({ label, children }: { label: string; children: ReactNode }) {
  return <div className="trades-mobile-sheet__row">
    <span className="trades-mobile-sheet__label">{label}</span>
    <span className="trades-mobile-sheet__value">{children}</span>
  </div>;
}

function MobileTradeSheetContent({
  trade,
  onEdit,
  onDelete,
  onClose
}: {
  trade: PortfolioTrade;
  onEdit: () => void;
  onDelete: () => Promise<void>;
  onClose: () => void;
}) {
  const sideLabel = trade.side === "Buy" ? "Покупка" : "Продажа";
  const sideType = trade.side === "Buy" ? "success" : "danger";

  return <section className="trades-mobile-sheet__content" role="dialog" aria-modal="true" aria-labelledby="trades-mobile-sheet-title">
    <div className="trades-mobile-sheet__handle" aria-hidden="true" />
    <header className="trades-mobile-sheet__header">
      <div className="trades-mobile-sheet__identity">
        <Typography.Title id="trades-mobile-sheet-title" level={4}>{trade.shortName || trade.secId}</Typography.Title>
        <Typography.Text type="secondary">ISIN: {trade.secId} · {trade.currencyId}</Typography.Text>
      </div>
      <div className="trades-mobile-sheet__actions">
        <Button type="text" aria-label={`Редактировать сделку ${trade.shortName || trade.secId}`} icon={<EditOutlined />} onClick={onEdit} />
        <Popconfirm title="Удалить сделку?" okText="Удалить" cancelText="Отмена" placement="bottomRight" onConfirm={onDelete}>
          <Button type="text" danger aria-label={`Удалить сделку ${trade.shortName || trade.secId}`} icon={<DeleteOutlined />} />
        </Popconfirm>
        <Button type="text" aria-label="Закрыть сделку" icon={<CloseOutlined />} onClick={onClose} />
      </div>
    </header>
    <div className="trades-mobile-sheet__rows">
      <MobileTradeDetailRow label="Дата">{trade.tradeDate}</MobileTradeDetailRow>
      <MobileTradeDetailRow label="Тип">
        <Typography.Text type={sideType}>{sideLabel}</Typography.Text>
      </MobileTradeDetailRow>
      <MobileTradeDetailRow label="Количество">{integer.format(trade.quantity)} шт.</MobileTradeDetailRow>
      <MobileTradeDetailRow label="Цена">{number.format(trade.price)} {trade.currencyId}</MobileTradeDetailRow>
      <MobileTradeDetailRow label="НКД за 1 шт.">{number.format(trade.accruedInterest)} {trade.currencyId}</MobileTradeDetailRow>
      <MobileTradeDetailRow label="Комиссия">{formatTradeMoney(trade.commission, trade.currencyId)}</MobileTradeDetailRow>
      <MobileTradeDetailRow label="Сумма">{formatTradeMoney(trade.amount, trade.currencyId)}</MobileTradeDetailRow>
    </div>
  </section>;
}

export default function TradesPage() {
  const [form] = Form.useForm<TradeForm>();
  const [rows, setRows] = useState<PortfolioTrade[]>([]);
  const [bonds, setBonds] = useState<BondSearchResult[]>([]);
  const [selectedBond, setSelectedBond] = useState<BondSearchResult>();
  const [editingId, setEditingId] = useState<string>();
  const [loading, setLoading] = useState(true);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [mobileTrade, setMobileTrade] = useState<PortfolioTrade | null>(null);
  const [mobileSheetOpen, setMobileSheetOpen] = useState(false);
  const { message } = AntdApp.useApp();

  const load = async () => {
    setLoading(true);
    try {
      const trades = await portfolioLedgerApi.getTrades();
      setRows(trades);
      setCurrentPage((page) => Math.min(page, Math.max(1, Math.ceil(trades.length / pageSize))));
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось загрузить сделки");
    } finally {
      setLoading(false);
    }
  };
  useEffect(() => {
    form.setFieldsValue({ tradeDate: dayjs() });
    void load();
  }, []);

  const searchBonds = async (value: string) => { if (value.trim().length < MIN_BOND_SEARCH_LENGTH) { setBonds([]); return; } try { setBonds(await portfolioLedgerApi.searchBonds(value)); } catch (error) { message.error(error instanceof Error ? error.message : "Не удалось выполнить поиск"); } };

  const save = async (values: TradeForm) => {
    if (!selectedBond) { message.error("Выберите облигацию из поиска"); return; }
    if (values.accruedInterestTotal == null) { message.error("Укажите НКД по сделке"); return; }
    try {
      await portfolioLedgerApi.saveTrade({ secId: selectedBond.secId, boardId: selectedBond.boardId, currencyId: selectedBond.currency, tradeDate: values.tradeDate.format("YYYY-MM-DD"), side: values.side, quantity: values.quantity, price: values.price, faceValue: selectedBond.faceValue, accruedInterestTotal: values.accruedInterestTotal, commissionPercent: values.commission ?? 0 }, editingId);
      message.success(editingId ? "Сделка обновлена" : "Сделка сохранена"); form.resetFields(); setSelectedBond(undefined); setEditingId(undefined); await load();
    } catch (error) { message.error(error instanceof Error ? error.message : "Не удалось сохранить сделку"); }
  };

  const startEditingTrade = (row: PortfolioTrade) => {
    const bond = { secId: row.secId, boardId: row.boardId, currency: row.currencyId, shortName: row.shortName, accruedInterest: row.accruedInterest, faceValue: row.faceValue };
    setEditingId(row.id);
    setSelectedBond(bond);
    setBonds((current) => current.some((item) => bondKey(item) === bondKey(bond)) ? current : [...current, bond]);
    form.setFieldsValue({ secId: bondKey(bond), tradeDate: dayjs(row.tradeDate), side: row.side, quantity: row.quantity, price: row.price, accruedInterestTotal: row.accruedInterest * row.quantity, commission: row.commissionPercent });
    setMobileSheetOpen(false);
  };

  const removeTrade = async (row: PortfolioTrade, closeSheet = false) => {
    try {
      await portfolioLedgerApi.deleteTrade(row.id);
      if (closeSheet) setMobileSheetOpen(false);
      await load();
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось удалить сделку");
    }
  };

  const columns: ColumnsType<PortfolioTrade> = [
    { title: "Дата", dataIndex: "tradeDate", width: 120, render: (value: string) => <span style={{ whiteSpace: "nowrap" }}>{value}</span> },
    { title: "Облигация", key: "bond", width: 190, render: (_, row) => <div><div>{row.shortName || row.secId}</div><Typography.Text type="secondary" style={{ whiteSpace: "nowrap" }}>{row.secId} · {row.currencyId}</Typography.Text></div> },
    { title: "Тип", dataIndex: "side", render: (value: TradeSide) => <Typography.Text type={value === "Buy" ? "success" : "danger"}>{value === "Buy" ? "Покупка" : "Продажа"}</Typography.Text> }, { title: "Количество", dataIndex: "quantity", render: (value: number) => integer.format(value) },
    { title: "Цена", dataIndex: "price", render: (value: number) => number.format(value) }, { title: "НКД за 1 шт.", dataIndex: "accruedInterest", render: (value: number) => number.format(value) }, { title: "Комиссия", dataIndex: "commission", render: (value: number) => number.format(value) }, { title: "Сумма", dataIndex: "amount", render: (value: number) => number.format(value) },
    { title: "Действия", width: 100, align: "center", render: (_, row) => <Space size={4}><Tooltip title="Изменить"><Button type="text" size="small" icon={<EditOutlined />} aria-label="Изменить" onClick={() => startEditingTrade(row)} /></Tooltip><Tooltip title="Удалить"><Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label="Удалить" onClick={() => void removeTrade(row)} /></Tooltip></Space> }
  ];
  const visibleTrades = rows.slice((currentPage - 1) * pageSize, currentPage * pageSize);
  const handlePageChange = (page: number, nextPageSize: number) => {
    setCurrentPage(page);
    setPageSize(nextPageSize);
  };

  return <main className="app-content trades-page">
    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 16, flexWrap: "wrap" }}>
      <Typography.Title level={1} style={{ margin: 0 }}>Сделки</Typography.Title>
    </div>
    <Typography.Paragraph type="secondary">Сделка хранится в валюте торгов, выбранной из карточки облигации. Позиции пересчитываются автоматически.</Typography.Paragraph>
    <Space orientation="vertical" size="large" style={{ width: "100%" }}>
      <Card title={editingId ? "Изменить сделку" : "Добавить сделку"} className="trade-editor-card">
        <Form form={form} layout="vertical" onFinish={save} initialValues={{ side: "Buy", accruedInterestTotal: 0, commission: 0.04 }}>
          <Form.Item label="Облигация" name="secId" rules={[{ required: true, message: "Выберите облигацию" }]}>
            <Select showSearch filterOption={false} onSearch={(value) => void searchBonds(value)} onChange={(value) => { const bond = bonds.find((item) => bondKey(item) === value); setSelectedBond(bond); form.setFieldValue("accruedInterestTotal", calculateAccruedInterestTotal(bond?.accruedInterest, form.getFieldValue("quantity"))); }} options={bonds.map((bond) => ({ value: bondKey(bond), label: `${bond.shortName || bond.secId} · ${bond.secId} · ${bond.currency} · номинал ${number.format(bond.faceValue)}` }))} placeholder="Поиск облигации" style={{ width: "100%" }} />
          </Form.Item>
          <Row gutter={[16, 0]}>
            <Col xs={24} sm={12} lg={4}><Form.Item label="Дата" name="tradeDate" rules={[{ required: true, message: "Укажите дату" }]}><DatePicker style={{ width: "100%" }} /></Form.Item></Col>
            <Col xs={24} sm={12} lg={4}><Form.Item label="Тип" name="side" rules={[{ required: true, message: "Выберите тип" }]}><Select options={[{ value: "Buy", label: "Покупка" }, { value: "Sell", label: "Продажа" }]} /></Form.Item></Col>
            <Col xs={24} sm={12} lg={4}><Form.Item label="Количество" name="quantity" rules={[{ required: true, message: "Укажите целое количество" }]}><InputNumber min={1} precision={0} style={{ width: "100%" }} placeholder="Шт." onChange={(quantity) => { if (selectedBond?.accruedInterest != null) form.setFieldValue("accruedInterestTotal", calculateAccruedInterestTotal(selectedBond.accruedInterest, quantity)); }} /></Form.Item></Col>
            <Col xs={24} sm={12} lg={4}><Form.Item label="Цена" name="price" rules={[{ required: true, message: "Укажите цену" }]}><InputNumber min={0} precision={4} style={{ width: "100%" }} placeholder="За 1 шт." /></Form.Item></Col>
            <Col xs={24} sm={12} lg={4}><Form.Item label="НКД" name="accruedInterestTotal" help={selectedBond && selectedBond.accruedInterest == null ? "НКД не указан в справочнике. Введите сумму вручную." : undefined} rules={[{ required: true, message: "Укажите НКД по сделке" }]}><InputNumber min={0} precision={4} style={{ width: "100%" }} placeholder="За все бумаги" /></Form.Item></Col>
            <Col xs={24} sm={12} lg={4}><Form.Item label="Комиссия, %"><Space.Compact block><Form.Item name="commission" noStyle rules={[{ required: true, message: "Укажите комиссию" }]}><InputNumber min={0} precision={4} style={{ width: "calc(100% - 40px)" }} placeholder="0,0000" /></Form.Item><Input value="%" disabled style={{ width: 40, textAlign: "center" }} /></Space.Compact></Form.Item></Col>
            <Col span={24} style={{ display: "flex", justifyContent: "flex-end" }}>
              <Space>{editingId && <Button onClick={() => { setEditingId(undefined); setSelectedBond(undefined); form.resetFields(); }}>Отмена</Button>}<Button type="primary" htmlType="submit">Сохранить</Button></Space>
            </Col>
          </Row>
        </Form>
      </Card>
      <Card title="Журнал сделок" className="trades-journal-card">
        <div className="trades-desktop-table">
          <Table rowKey="id" loading={loading} columns={columns} dataSource={rows} pagination={{ current: currentPage, pageSize, showSizeChanger: true, pageSizeOptions: tradePageSizeOptions, onChange: handlePageChange }} />
        </div>
        <section className="trades-mobile-list" aria-label="Журнал сделок">
          <div className="trades-mobile-list__head" aria-hidden="true">
            <span>Дата</span>
            <span>Бумага</span>
            <span>Тип</span>
            <span>Количество</span>
            <span>Сумма</span>
          </div>
          {loading && rows.length === 0 ? <div className="trades-mobile-list__state"><Spin /></div> : null}
          {!loading && rows.length === 0 ? <Typography.Text className="trades-mobile-list__empty" type="secondary">Сделок пока нет</Typography.Text> : null}
          <div className="trades-mobile-list__items" role="list" aria-label="Сделки">
            {visibleTrades.map((row) => {
              const isSelected = mobileTrade?.id === row.id;
              const sideLabel = row.side === "Buy" ? "Покупка" : "Продажа";
              return <div className="trades-mobile-list__item" key={row.id} role="listitem">
                <button
                  className={`trades-mobile-row${isSelected ? " trades-mobile-row--selected" : ""}`}
                  type="button"
                  aria-haspopup="dialog"
                  aria-expanded={isSelected && mobileSheetOpen}
                  aria-label={`${row.tradeDate}, ${row.shortName || row.secId}, ${sideLabel}, ${integer.format(row.quantity)} шт., ${formatTradeMoney(row.amount, row.currencyId)}`}
                  onClick={() => { setMobileTrade(row); setMobileSheetOpen(true); }}
                >
                  <span className="trades-mobile-row__date">{row.tradeDate}</span>
                  <span className="trades-mobile-row__bond">
                    <strong>{row.shortName || row.secId}</strong>
                    <small>{row.secId}</small>
                  </span>
                  <span className={`trades-mobile-row__side trades-mobile-row__side--${row.side === "Buy" ? "buy" : "sell"}`}>{sideLabel}</span>
                  <span className="trades-mobile-row__quantity">{integer.format(row.quantity)} шт.</span>
                  <strong className="trades-mobile-row__amount">{formatTradeMoney(row.amount, row.currencyId)}</strong>
                </button>
              </div>;
            })}
          </div>
          <Pagination current={currentPage} pageSize={pageSize} total={rows.length} showSizeChanger pageSizeOptions={tradePageSizeOptions} onChange={handlePageChange} className="trades-mobile-pagination" />
        </section>
      </Card>
    </Space>
    <Popup
      visible={mobileSheetOpen}
      position="bottom"
      closeOnSwipe
      closeOnMaskClick
      bodyClassName="trades-mobile-sheet"
      bodyStyle={{ bottom: "calc(64px + env(safe-area-inset-bottom))", maxHeight: "calc(100dvh - 64px - env(safe-area-inset-bottom))", overflowY: "auto" }}
      onClose={() => setMobileSheetOpen(false)}
      afterClose={() => setMobileTrade(null)}
    >
      {mobileTrade == null ? null : <MobileTradeSheetContent
        trade={mobileTrade}
        onEdit={() => startEditingTrade(mobileTrade)}
        onDelete={() => removeTrade(mobileTrade, true)}
        onClose={() => setMobileSheetOpen(false)}
      />}
    </Popup>
  </main>;
}
