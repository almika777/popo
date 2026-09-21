"use client";

import { Alert, Button, Collapse, Progress, Space, Tag, Typography } from "antd";
import { useCallback, useEffect, useMemo, useState } from "react";
import { initializationApi, type InitializationJobState, type InitializationStatus } from "../lib/initialization-api";

const statusLabels = {
  Pending: "Ожидает",
  Running: "Выполняется",
  Succeeded: "Готово",
  Failed: "Ошибка"
} as const;

const statusColors = {
  Pending: "default",
  Running: "processing",
  Succeeded: "success",
  Failed: "error"
} as const;

function formatTime(value: string | null) {
  if (!value) return "—";
  return new Intl.DateTimeFormat("ru-RU", { dateStyle: "short", timeStyle: "medium" }).format(new Date(value));
}

function JobStatusRow({ job, onRetry, retrying }: { job: InitializationJobState; onRetry: (jobKey: string) => void; retrying: boolean }) {
  return (
    <div className="initialization-job">
      <div className="initialization-job-header">
        <Space wrap>
          <Typography.Text strong>{job.displayName}</Typography.Text>
          <Tag color={statusColors[job.status]}>{statusLabels[job.status]}</Tag>
        </Space>
        {job.status === "Failed" && (
          <Button size="small" loading={retrying} onClick={() => onRetry(job.jobKey)}>
            Перезапустить
          </Button>
        )}
      </div>
      <Progress
        percent={job.progressPercent ?? undefined}
        status={job.status === "Failed" ? "exception" : job.status === "Succeeded" ? "success" : "active"}
        showInfo={job.progressPercent !== null}
      />
      <div className="initialization-job-details">
        <span>{job.phase ?? "Ожидание запуска"}</span>
        {job.totalItems !== null && <span>{job.processedItems} / {job.totalItems}</span>}
        <span>Обновлено: {formatTime(job.updatedAt)}</span>
      </div>
      {job.lastError && <Typography.Text type="danger">{job.lastError}</Typography.Text>}
    </div>
  );
}

export function InitializationStatusBanner() {
  const [status, setStatus] = useState<InitializationStatus | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [retryingJobKey, setRetryingJobKey] = useState<string | null>(null);

  const loadStatus = useCallback(async () => {
    try {
      setStatus(await initializationApi.getStatus());
      setLoadError(null);
    } catch (error) {
      setLoadError(error instanceof Error ? error.message : "Не удалось получить статус инициализации.");
    }
  }, []);

  useEffect(() => {
    void loadStatus();
    const timer = window.setInterval(() => void loadStatus(), 3000);
    return () => window.clearInterval(timer);
  }, [loadStatus]);

  const completedCount = useMemo(
    () => status?.jobs.filter((job) => job.status === "Succeeded").length ?? 0,
    [status]
  );

  const retryJob = async (jobKey: string) => {
    setRetryingJobKey(jobKey);
    try {
      setStatus(await initializationApi.retryJob(jobKey));
      setLoadError(null);
    } catch (error) {
      setLoadError(error instanceof Error ? error.message : "Не удалось перезапустить Job.");
    } finally {
      setRetryingJobKey(null);
    }
  };

  if (!status?.isVisible) {
    return loadError ? <Alert className="initialization-status-error" type="warning" showIcon message={loadError} /> : null;
  }

  return (
    <section className="initialization-status" aria-label="Первичная инициализация данных">
      <Alert
        type="warning"
        showIcon
        message="Идёт первичная инициализация данных"
        description="Приложение уже доступно, но до завершения всех загрузок данные и расчёты могут быть неполными или некорректными. Навигация не заблокирована."
      />
      <Collapse
        className="initialization-status-details"
        items={[{
          key: "jobs",
          label: `Состояние загрузок: ${completedCount} из ${status.jobs.length} завершено`,
          children: (
            <Space direction="vertical" size={16} className="initialization-jobs-list">
              {status.jobs.map((job) => (
                <JobStatusRow
                  key={job.jobKey}
                  job={job}
                  onRetry={retryJob}
                  retrying={retryingJobKey === job.jobKey}
                />
              ))}
              <Typography.Text type="secondary">
                Перезапуск одной Job не запускает связанные Jobs автоматически.
              </Typography.Text>
            </Space>
          )
        }]}
      />
      {loadError && <Alert type="error" showIcon message={loadError} />}
    </section>
  );
}
