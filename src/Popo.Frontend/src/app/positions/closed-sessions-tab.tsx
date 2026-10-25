"use client";

import { useEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { Alert, Button, Card, Descriptions, Pagination, Space, Spin, Table, Tag, Tooltip, Typography } from "antd";
import { Popup } from "antd-mobile";
import type { ColumnsType } from "antd/es/table";
import dayjs from "dayjs";
import { closedPositionSessionsApi, type ClosedPositionSession } from "../../lib/closed-position-sessions-api";
import { getVisibleClosedPositionSessions } from "./closed-position-sessions-mobile";
import styles from "./closed-sessions-tab.module.css";

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

function MobileClosedSessionDetailRow({ label, children }: { label: string; children: ReactNode }) {
  return <div className={styles.sheetRow}>
    <span className={styles.sheetLabel}>{label}</span>
    <span className={styles.sheetValue}>{children}</span>
  </div>;
}

function MobileClosedSessionSheetContent({ row }: { row: ClosedPositionSession }) {
  const bondName = row.shortName || row.secId;

  return <section className={styles.sheetContent} role="dialog" aria-modal="true" aria-labelledby="closed-session-sheet-title">
    <div className={styles.sheetHandle} aria-hidden="true" />
    <header className={styles.sheetHeader}>
      <div className={styles.sheetIdentity}>
        <Typography.Title id="closed-session-sheet-title" level={4}>{bondName}</Typography.Title>
        <Typography.Text type="secondary">{row.secId} · {row.currencyId}</Typography.Text>
      </div>
    </header>
    {!row.isComplete && <Alert type="warning" showIcon title={row.incompleteReason || "Недостаточно данных для полного расчёта"} />}
    <div className={styles.sheetRows}>
      <MobileClosedSessionDetailRow label="Дата покупки">{dayjs(row.startDate).format("DD.MM.YYYY")}</MobileClosedSessionDetailRow>
      <MobileClosedSessionDetailRow label="Дата продажи">{dayjs(row.endDate).format("DD.MM.YYYY")}</MobileClosedSessionDetailRow>
      <MobileClosedSessionDetailRow label="Дней владения">{row.holdingDays}</MobileClosedSessionDetailRow>
      <MobileClosedSessionDetailRow label="Итого">{renderResult(row.totalPnl, row)}</MobileClosedSessionDetailRow>
      <MobileClosedSessionDetailRow label="Итого, %">{renderResult(row.totalPnlPercent, row, true)}</MobileClosedSessionDetailRow>
      <MobileClosedSessionDetailRow label="Годовых, оценка">{renderResult(row.annualizedPnlPercent, row, true)}</MobileClosedSessionDetailRow>
      <MobileClosedSessionDetailRow label="НКД, оценка">{renderResult(row.couponIncome, row)}</MobileClosedSessionDetailRow>
      <MobileClosedSessionDetailRow label="Результат тела">{renderResult(row.bodyPnl, row)}</MobileClosedSessionDetailRow>
      <MobileClosedSessionDetailRow label="Куплено бумаг">{quantity.format(row.boughtQuantity)} шт.</MobileClosedSessionDetailRow>
      <MobileClosedSessionDetailRow label="Сумма покупок без НКД">{number.format(row.boughtCleanAmount)} {row.currencyId}</MobileClosedSessionDetailRow>
      <MobileClosedSessionDetailRow label="Сумма продаж без НКД">{number.format(row.soldCleanAmount)} {row.currencyId}</MobileClosedSessionDetailRow>
      <MobileClosedSessionDetailRow label="Комиссии">{number.format(row.commission)} {row.currencyId}</MobileClosedSessionDetailRow>
      <MobileClosedSessionDetailRow label="Амортизация">{renderResult(row.amortizationIncome, row)}</MobileClosedSessionDetailRow>
      <MobileClosedSessionDetailRow label="Рассчитано">{dayjs(row.calculatedAt).format("DD.MM.YYYY HH:mm")}</MobileClosedSessionDetailRow>
    </div>
  </section>;
}

export default function ClosedPositionSessionsTab() {
  const [rows, setRows] = useState<ClosedPositionSession[]>([]);
  const [loading, setLoading] = useState(true);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [mobileSession, setMobileSession] = useState<ClosedPositionSession | null>(null);
  const [mobileSheetOpen, setMobileSheetOpen] = useState(false);
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
          setCurrentPage((page) => Math.min(page, Math.max(1, Math.ceil(data.length / pageSize))));
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
  const visibleRows = getVisibleClosedPositionSessions(rows, currentPage, pageSize);
  const handlePageChange = (page: number, nextPageSize: number) => {
    setCurrentPage(Math.min(page, Math.max(1, Math.ceil(rows.length / nextPageSize))));
    setPageSize(nextPageSize);
  };

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
          className={styles.desktopTable}
          rowKey="id"
          columns={columns}
          dataSource={rows}
          loading={loading}
          scroll={{ x: 790 }}
          locale={{ emptyText: "Закрытых сессий пока нет. Запустите пересчёт." }}
          pagination={{ current: currentPage, pageSize, showSizeChanger: true, pageSizeOptions: [10, 20, 50, 100], onChange: handlePageChange }}
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
        <section className={styles.mobileList} aria-label="Закрытые сессии позиций">
          {loading && rows.length === 0 ? <div className={styles.mobileState}><Spin /></div> : null}
          {!loading && rows.length === 0 ? <Typography.Text className={styles.mobileEmpty} type="secondary">Закрытых сессий пока нет. Запустите пересчёт.</Typography.Text> : null}
          <div className={styles.mobileItems} role="list" aria-label="Закрытые сессии">
            {visibleRows.map((row) => {
              const bondName = row.shortName || row.secId;
              const selected = mobileSession?.id === row.id && mobileSheetOpen;
              return <div key={row.id} role="listitem">
                <button
                  className={`${styles.mobileRow}${selected ? ` ${styles.mobileRowSelected}` : ""}`}
                  type="button"
                  aria-haspopup="dialog"
                  aria-expanded={selected}
                  aria-label={`${bondName}, ${row.holdingDays} дней владения, итог ${row.totalPnl == null ? "недоступен" : `${number.format(row.totalPnl)} ${row.currencyId}`}`}
                  onClick={() => {
                    setMobileSession(row);
                    setMobileSheetOpen(true);
                  }}
                >
                  <span className={styles.mobileIdentity}>
                    <strong>{bondName}</strong>
                    <small>{row.secId} · {row.currencyId}</small>
                    {!row.isComplete && <Tag color="orange">Неполный расчёт</Tag>}
                  </span>
                  <span className={styles.mobileOutcome}>
                    <strong>{renderResult(row.totalPnl, row)}</strong>
                    <small>{renderResult(row.totalPnlPercent, row, true)}</small>
                  </span>
                  <span className={styles.mobileDays}>{row.holdingDays} дн. владения</span>
                  <span className={styles.mobileAnnualized}>Годовых {renderResult(row.annualizedPnlPercent, row, true)}</span>
                </button>
              </div>;
            })}
          </div>
          <Pagination
            className={styles.mobilePagination}
            current={currentPage}
            pageSize={pageSize}
            total={rows.length}
            showSizeChanger
            pageSizeOptions={[10, 20, 50, 100]}
            onChange={handlePageChange}
          />
        </section>
      </Space>
    </Card>
    <Popup
      visible={mobileSheetOpen}
      position="bottom"
      closeOnSwipe
      closeOnMaskClick
      showCloseButton
      bodyClassName="closed-sessions-mobile-sheet"
      bodyStyle={{ bottom: "calc(64px + env(safe-area-inset-bottom))", maxHeight: "calc(100dvh - 64px - env(safe-area-inset-bottom))", overflowY: "auto" }}
      onClose={() => setMobileSheetOpen(false)}
      afterClose={() => setMobileSession(null)}
    >
      {mobileSession == null ? null : <MobileClosedSessionSheetContent row={mobileSession} />}
    </Popup>
  </Space>;
}
