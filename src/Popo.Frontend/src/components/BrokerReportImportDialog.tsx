"use client";

import { useEffect, useMemo, useState } from "react";
import dayjs from "dayjs";
import { Alert, App as AntdApp, Button, DatePicker, Input, InputNumber, Modal, Select, Space, Table, Typography } from "antd";
import type { ColumnsType } from "antd/es/table";
import { UploadOutlined } from "@ant-design/icons";
import { portfolioLedgerApi, type BrokerReportOperation, type BrokerReportOperationKind, type TradeSide } from "../lib/portfolio-ledger-api";

type BrokerReportImportDialogProps = {
  open: boolean;
  onClose: () => void;
  onImported: () => void;
};

const amountFormat = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 6 });
const operationLabel: Record<BrokerReportOperationKind, string> = {
  BondTrade: "Облигация",
  FundOperation: "Фонд",
  Deposit: "Пополнение",
  Withdrawal: "Вывод"
};

export default function BrokerReportImportDialog({ open, onClose, onImported }: BrokerReportImportDialogProps) {
  const [file, setFile] = useState<File>();
  const [operations, setOperations] = useState<BrokerReportOperation[]>();
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [duplicatesHidden, setDuplicatesHidden] = useState(0);
  const [outsideHistoryHidden, setOutsideHistoryHidden] = useState(0);
  const [unsupportedOperationsHidden, setUnsupportedOperationsHidden] = useState(0);
  const [loadingPreview, setLoadingPreview] = useState(false);
  const [importing, setImporting] = useState(false);
  const { message } = AntdApp.useApp();

  useEffect(() => {
    if (!open) return;
    setFile(undefined);
    setOperations(undefined);
    setSelectedIds([]);
    setDuplicatesHidden(0);
    setOutsideHistoryHidden(0);
    setUnsupportedOperationsHidden(0);
  }, [open]);

  const selectedOperations = useMemo(
    () => (operations ?? []).filter(x => selectedIds.includes(x.id) && x.canImport),
    [operations, selectedIds]
  );

  const updateOperation = (id: string, values: Partial<BrokerReportOperation>) =>
    setOperations(current => current?.map(operation => operation.id === id ? { ...operation, ...values } : operation));

  const preview = async () => {
    if (!file) return;
    if (!file.name.toLowerCase().endsWith(".pdf")) {
      message.error("Выберите PDF-файл брокерского отчёта.");
      return;
    }

    setLoadingPreview(true);
    try {
      const result = await portfolioLedgerApi.previewBrokerReport(file);
      setOperations(result.operations);
      setSelectedIds(result.operations.filter(x => x.canImport).map(x => x.id));
      setDuplicatesHidden(result.duplicatesHidden);
      setOutsideHistoryHidden(result.outsideHistoryHidden);
      setUnsupportedOperationsHidden(result.unsupportedOperationsHidden);
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось разобрать брокерский отчёт.");
    } finally {
      setLoadingPreview(false);
    }
  };

  const importSelected = async () => {
    setImporting(true);
    try {
      const result = await portfolioLedgerApi.importBrokerReportOperations(selectedOperations.map(({ description, canImport, warning, ...operation }) => operation));
      message.success(`Добавлено: облигаций — ${result.tradesAdded}, операций фондов — ${result.fundOperationsAdded}, движений денег — ${result.cashFlowsAdded}.`);
      onImported();
      onClose();
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Не удалось добавить выбранные операции.");
    } finally {
      setImporting(false);
    }
  };

  const columns: ColumnsType<BrokerReportOperation> = [
    {
      title: "Операция",
      dataIndex: "kind",
      width: 135,
      render: (kind: BrokerReportOperationKind, row) => <Space direction="vertical" size={0}>
        <span>{operationLabel[kind]}</span>
        <Typography.Text type="secondary">{row.description}</Typography.Text>
      </Space>
    },
    {
      title: "Дата",
      dataIndex: "date",
      width: 145,
      render: (value: string, row) => <DatePicker
        value={dayjs(value)}
        format="DD.MM.YYYY"
        onChange={date => date && updateOperation(row.id, { date: date.format("YYYY-MM-DD") })}
      />
    },
    {
      title: "Код бумаги / фонд",
      dataIndex: "secId",
      width: 170,
      render: (value: string | null, row) => value === null ? "—" : <Space direction="vertical" size={4}>
        <Input value={value} onChange={event => updateOperation(row.id, { secId: event.target.value })} />
        {row.kind === "BondTrade" && <Space size={4}>
          <Input value={row.boardId ?? ""} aria-label="Режим торгов" placeholder="Режим" onChange={event => updateOperation(row.id, { boardId: event.target.value })} />
          <Input value={row.currencyId ?? ""} aria-label="Валюта" placeholder="Валюта" onChange={event => updateOperation(row.id, { currencyId: event.target.value })} />
        </Space>}
      </Space>
    },
    {
      title: "Направление",
      dataIndex: "side",
      width: 135,
      render: (value: TradeSide | null, row) => value === null ? "—" : <Select
        value={value}
        style={{ width: 120 }}
        options={[{ value: "Buy", label: "Покупка" }, { value: "Sell", label: "Продажа" }]}
        onChange={side => updateOperation(row.id, { side })}
      />
    },
    {
      title: "Количество",
      dataIndex: "quantity",
      width: 115,
      render: (value: number | null, row) => value === null ? "—" : <InputNumber
        min={1}
        precision={0}
        value={value}
        style={{ width: 100 }}
        onChange={quantity => updateOperation(row.id, { quantity: quantity ?? undefined })}
      />
    },
    {
      title: "Цена за 1 шт.",
      dataIndex: "unitPrice",
      width: 135,
      render: (value: number | null, row) => value === null ? "—" : <InputNumber
        min={0.000001}
        precision={6}
        value={value}
        style={{ width: 120 }}
        onChange={unitPrice => updateOperation(row.id, { unitPrice: unitPrice ?? undefined })}
      />
    },
    {
      title: "Номинал",
      dataIndex: "faceValue",
      width: 115,
      render: (value: number | null, row) => row.kind !== "BondTrade" ? "—" : <InputNumber
        min={0.000001}
        precision={6}
        value={value ?? undefined}
        style={{ width: 100 }}
        onChange={faceValue => updateOperation(row.id, { faceValue: faceValue ?? undefined })}
      />
    },
    {
      title: "НКД по сделке",
      dataIndex: "accruedInterestTotal",
      width: 140,
      render: (value: number | null, row) => row.kind !== "BondTrade" ? "—" : <InputNumber
        min={0}
        precision={6}
        value={value ?? undefined}
        style={{ width: 125 }}
        onChange={accruedInterestTotal => updateOperation(row.id, { accruedInterestTotal: accruedInterestTotal ?? undefined })}
      />
    },
    {
      title: "Комиссия",
      dataIndex: "commission",
      width: 125,
      render: (value: number | null, row) => row.kind === "Deposit" || row.kind === "Withdrawal" ? "—" : <InputNumber
        min={0}
        precision={6}
        value={value ?? 0}
        style={{ width: 110 }}
        onChange={commission => updateOperation(row.id, { commission: commission ?? undefined })}
      />
    },
    {
      title: "Сумма, RUB",
      dataIndex: "amount",
      width: 140,
      render: (value: number | null, row) => row.kind !== "Deposit" && row.kind !== "Withdrawal" ? "—" : <InputNumber
        min={0.01}
        precision={2}
        value={value ?? undefined}
        style={{ width: 125 }}
        onChange={amount => updateOperation(row.id, { amount: amount ?? undefined })}
      />
    },
    {
      title: "Примечание",
      dataIndex: "warning",
      width: 260,
      render: (warning: string | null) => warning
        ? <Typography.Text type="warning">{warning}</Typography.Text>
        : "—"
    }
  ];

  const footer = operations === undefined
    ? <Space>
      <Button onClick={onClose}>Отмена</Button>
      <Button type="primary" icon={<UploadOutlined />} loading={loadingPreview} disabled={!file} onClick={() => void preview()}>
        Проверить отчёт
      </Button>
    </Space>
    : <Space>
      <Button onClick={onClose}>Отмена</Button>
      <Button type="primary" loading={importing} disabled={selectedOperations.length === 0} onClick={() => void importSelected()}>
        Добавить выбранные ({selectedOperations.length})
      </Button>
    </Space>;

  return <Modal
    open={open}
    title="Импорт операций из брокерского отчёта"
    width="96vw"
    onCancel={onClose}
    footer={footer}
    destroyOnHidden
  >
    {operations === undefined ? <Space direction="vertical" size="middle" style={{ width: "100%" }}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
        Загрузите PDF-отчёт Т-Инвестиции. Сделки и движения денег будут только показаны для проверки; данные попадут в портфель после подтверждения.
      </Typography.Paragraph>
      <Input
        type="file"
        accept="application/pdf,.pdf"
        aria-label="PDF-файл брокерского отчёта"
        onChange={event => setFile(event.target.files?.[0])}
      />
    </Space> : <Space direction="vertical" size="middle" style={{ width: "100%" }}>
      <Alert
        type="info"
        showIcon
        message={`Найдено операций для проверки: ${operations.length}. Скрыто повторов: ${duplicatesHidden}; старше истории портфеля: ${outsideHistoryHidden}.`}
      />
      {unsupportedOperationsHidden > 0 && <Alert
        type="warning"
        showIcon
        message={`Пропущено неподдерживаемых операций с бумагами: ${unsupportedOperationsHidden}.`}
      />}
      {(operations.length === 0) ? <Alert type="success" showIcon message="Новых операций для добавления не найдено." /> : <Table
        rowKey="id"
        size="small"
        columns={columns}
        dataSource={operations}
        pagination={{ pageSize: 10 }}
        scroll={{ x: 1500 }}
        rowSelection={{
          selectedRowKeys: selectedIds,
          onChange: keys => setSelectedIds(keys.map(String)),
          getCheckboxProps: row => ({ disabled: !row.canImport })
        }}
      />}
    </Space>}
  </Modal>;
}
