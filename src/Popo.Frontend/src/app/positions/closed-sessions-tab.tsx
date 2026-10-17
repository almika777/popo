"use client";

import { useEffect, useRef, useState } from "react";
import { Alert, Button, Card, Descriptions, Space, Table, Tag, Tooltip, Typography } from "antd";
import type { ColumnsType } from "antd/es/table";
import dayjs from "dayjs";
import { closedPositionSessionsApi, type ClosedPositionSession } from "../../lib/closed-position-sessions-api";

const number = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const quantity = new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 8 });
const stateLabels: Record<string, string> = {
  Enqueued: "В очереди",
  Processing: "Пересчёт выполняется",
  Scheduled: "Ожидается запуск или повторная попытка",
  Awaiting: "Ожидается запуск"
};

function renderResult(value: number | null, row: ClosedPositionSession, percent = false) {
  if (value == null) {
    return <Tooltip title={row.incompleteReason || (percent && row.holdingDays === 0
      ? "Годовая оценка недоступна для сессии длительностью менее одного дня"
      : "Недостаточно данных для расчёта")}>
      <Typography.Text type="secondary">Недоступно</Typography.Text>
    </Tooltip>;
  }
  return <Typography.Text type={value >= 0 ? "success" : "danger"}>
    {value > 0 ? "+" : ""}{number.format(value)}{percent ? "%" : ` ${row.currencyId}`}
  </Typography.Text>;
}

export default function ClosedPositionSessionsTab() {
  const [rows, setRows] = useState<ClosedPositionSession[]>([]);
  const [loading, setLoading] = useState(true);
  const [pageSize, setPageSize] = useState(20);
  const [queueing, setQueueing] = useState(false);
  const [jobId, setJobId] = useState<string | null>(null);
  const [jobState, setJobState] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const queueRequest = useRef<AbortController | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    void closedPositionSessionsApi.getAll(controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setRows(data);
      })
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : "Не удалось загрузить сессии.");
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => {
      controller.abort();
      queueRequest.current?.abort();
    };
  }, []);

  useEffect(() => {
    if (!jobId) return;
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout> | undefined;

    const poll = async () => {
      try {
        const result = await closedPositionSessionsApi.getRecalculation(jobId, controller.signal);
        if (controller.signal.aborted) return;
        setJobState(result.state);
        if (result.state === "Succeeded") {
          const data = await closedPositionSessionsApi.getAll(controller.signal);
          if (controller.signal.aborted) return;
          setRows(data);
          setJobId(null);
          return;
        }
        if (result.state === "Failed" || result.state === "Deleted") {
          setError(result.state === "Failed" ? "Пересчёт завершился ошибкой. Повторите запуск." : "Задание пересчёта удалено. Повторите запуск.");
          setJobId(null);
          return;
        }
        timer = setTimeout(() => void poll(), 2000);
      } catch (cause) {
        if (controller.signal.aborted) return;
        setError(cause instanceof Error ? cause.message : "Не удалось получить результат пересчёта.");
        setJobId(null);
        setJobState(null);
      }
    };

    void poll();
    return () => {
      controller.abort();
      if (timer !== undefined) clearTimeout(timer);
    };
  }, [jobId]);

  const recalculate = async () => {
    if (queueRequest.current || jobId || loading) return;
    const controller = new AbortController();
    queueRequest.current = controller;
    setQueueing(true);
    setError(null);
    setJobState(null);
    try {
      const result = await closedPositionSessionsApi.recalculate(controller.signal);
      if (controller.signal.aborted) return;
      if (!result.jobId) throw new Error("Сервер не вернул идентификатор пересчёта.");
      setJobState("Enqueued");
      setJobId(result.jobId);
    } catch (cause) {
      if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : "Не удалось запустить пересчёт.");
    } finally {
      if (queueRequest.current === controller) queueRequest.current = null;
      if (!controller.signal.aborted) setQueueing(false);
    }
  };

  const columns: ColumnsType<ClosedPositionSession> = [
    { title: "Облигация", key: "bond", width: 210, render: (_, row) => <Space orientation="vertical" size={0}>
      <Typography.Text strong>{row.shortName || row.secId}</Typography.Text>
      <Typography.Text type="secondary">{row.secId} · {row.currencyId}</Typography.Text>
      {!row.isComplete && <Tooltip title={row.incompleteReason || "Недостаточно данных для полного расчёта"}><Tag color="orange">Неполный расчёт</Tag></Tooltip>}
    </Space> },
    { title: "Дней владения", dataIndex: "holdingDays", width: 125 },
    { title: "Итого", dataIndex: "totalPnl", width: 170, render: (value: number | null, row) => renderResult(value, row) },
    { title: "Итого, %", dataIndex: "totalPnlPercent", width: 115, render: (value: number | null, row) => renderResult(value, row, true) },
    { title: "Годовых, %", dataIndex: "annualizedPnlPercent", width: 130, render: (value: number | null, row) => renderResult(value, row, true) }
  ];

  return <Space orientation="vertical" size="large" style={{ width: "100%" }}>
    <Card title="Закрытые позиции" extra={<Button type="primary" loading={queueing || jobId !== null} disabled={loading} onClick={() => void recalculate()}>Пересчитать</Button>}>
      <Space orientation="vertical" size="middle" style={{ width: "100%" }}>
        <Typography.Paragraph type="secondary" style={{ margin: 0 }}>
          Каждая строка — отдельная закрытая сессия владения облигацией. НКД оценивается по каждому купонному периоду за дни владения с учётом количества бумаг.
          Комиссии вычтены из итогового результата; налоги и НКД в сделках не учитываются. Годовая доходность — линейная оценка: итоговый процент × 365 / дни владения, без реинвестирования.
        </Typography.Paragraph>
        {error && <Alert type="error" showIcon title={error} />}
        {jobId && <Alert type="info" showIcon title={stateLabels[jobState ?? ""] ?? "Ожидается завершение пересчёта"} />}
        {!jobId && jobState === "Succeeded" && <Alert type="success" showIcon title="Пересчёт завершён, данные обновлены" />}
        <Table<ClosedPositionSession>
          rowKey="id"
          columns={columns}
          dataSource={rows}
          loading={loading}
          scroll={{ x: 790 }}
          locale={{ emptyText: "Закрытых сессий пока нет. Запустите пересчёт." }}
          pagination={{ pageSize, showSizeChanger: true, pageSizeOptions: [10, 20, 50, 100], onChange: (_page, size) => setPageSize(size) }}
          expandable={{ expandedRowRender: (row) => <Space orientation="vertical" size="middle" style={{ width: "100%" }}>
            {!row.isComplete && <Alert type="warning" showIcon title={row.incompleteReason || "Недостаточно данных для полного расчёта"} />}
            <Descriptions bordered size="small" column={{ xs: 1, sm: 2, lg: 3 }} items={[
              { key: "startDate", label: "Начало", children: dayjs(row.startDate).format("DD.MM.YYYY") },
              { key: "endDate", label: "Закрытие", children: dayjs(row.endDate).format("DD.MM.YYYY") },
              { key: "couponIncome", label: "НКД, оценка", children: renderResult(row.couponIncome, row) },
              { key: "bodyPnl", label: "Результат тела", children: renderResult(row.bodyPnl, row) },
              { key: "quantity", label: "Куплено бумаг", children: quantity.format(row.boughtQuantity) },
              { key: "invested", label: "Сумма покупок без НКД", children: `${number.format(row.boughtCleanAmount)} ${row.currencyId}` },
              { key: "sold", label: "Сумма продаж без НКД", children: `${number.format(row.soldCleanAmount)} ${row.currencyId}` },
              { key: "commission", label: "Комиссии", children: `${number.format(row.commission)} ${row.currencyId}` },
              { key: "amortization", label: "Амортизация", children: renderResult(row.amortizationIncome, row) },
              { key: "calculated", label: "Рассчитано", children: dayjs(row.calculatedAt).format("DD.MM.YYYY HH:mm") }
            ]} />
          </Space> }}
        />
      </Space>
    </Card>
  </Space>;
}
