"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Image from "next/image";
import { Alert, Button, Card, Checkbox, Empty, Select, Spin, Typography, Upload, message } from "antd";
import type { UploadFile } from "antd";
import styles from "./issuer-logos.module.css";
import { issuerLogosApi, type IssuerLogo } from "../../../lib/issuer-logos-api";

export default function IssuerLogosPage() {
  const [issuers, setIssuers] = useState<IssuerLogo[]>([]);
  const [selectedEmitterId, setSelectedEmitterId] = useState<number>();
  const [fileList, setFileList] = useState<UploadFile[]>([]);
  const [missingOnly, setMissingOnly] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [messageApi, contextHolder] = message.useMessage();

  const loadIssuers = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    setError(null);
    try {
      setIssuers(await issuerLogosApi.getIssuers(signal));
    } catch (reason) {
      if (!signal?.aborted) setError(reason instanceof Error ? reason.message : "Не удалось загрузить эмитентов.");
    } finally {
      if (!signal?.aborted) setLoading(false);
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    void loadIssuers(controller.signal);
    return () => controller.abort();
  }, [loadIssuers]);

  const selectedIssuer = issuers.find(issuer => issuer.emitterId === selectedEmitterId);
  const visibleIssuers = useMemo(
    () => missingOnly ? issuers.filter(issuer => !issuer.hasLogo) : issuers,
    [issuers, missingOnly]
  );
  const options = visibleIssuers.map(issuer => ({
    value: issuer.emitterId,
    label: `${issuer.title || "Без названия"} · ${issuer.emitterId}`
  }));
  const file = fileList[0]?.originFileObj;

  const upload = async () => {
    if (selectedIssuer === undefined || file === undefined) return;
    setSaving(true);
    setError(null);
    try {
      await issuerLogosApi.upload(selectedIssuer.emitterId, file);
      setFileList([]);
      await loadIssuers();
      messageApi.success(`Логотип эмитента «${selectedIssuer.title}» сохранён.`);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Не удалось сохранить логотип.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <main className={`app-content ${styles.page}`}>
      {contextHolder}
      <header className={styles.heading}>
        <div>
          <Typography.Title level={1}>Логотипы эмитентов</Typography.Title>
          <Typography.Text type="secondary">Загрузка одного PNG или SVG для выбранного эмитента</Typography.Text>
        </div>
        <Button href="/statistics">К статистике</Button>
      </header>

      {error && <Alert type="error" showIcon message="Не удалось выполнить операцию" description={error} />}

      <Card title="Добавить логотип">
        {loading
          ? <div className={styles.loading}><Spin /></div>
          : issuers.length === 0
            ? <Empty description="Список эмитентов пуст" />
            : <div className={styles.form}>
                <Typography.Text type="secondary">Эмитент: логотип сохраняется под его EmitterId</Typography.Text>
                <Checkbox checked={missingOnly} onChange={event => setMissingOnly(event.target.checked)}>
                  Показать только без логотипа ({issuers.filter(issuer => !issuer.hasLogo).length})
                </Checkbox>
                <Select
                  showSearch
                  allowClear
                  value={selectedEmitterId}
                  options={options}
                  optionFilterProp="label"
                  placeholder="Найдите эмитента по названию или ID"
                  onChange={value => {
                    setSelectedEmitterId(value);
                    setFileList([]);
                  }}
                  aria-label="Эмитент"
                />
                {selectedIssuer && (
                  <div className={styles.selectedIssuer}>
                    {selectedIssuer.hasLogo
                      ? <Image
                          src={issuerLogosApi.imageUrl(selectedIssuer.emitterId)}
                          alt={`Логотип ${selectedIssuer.title}`}
                          width={64}
                          height={64}
                          unoptimized
                        />
                      : <span className={styles.placeholder}>{selectedIssuer.title.slice(0, 1) || "?"}</span>}
                    <div>
                      <Typography.Text strong>{selectedIssuer.title || "Без названия"}</Typography.Text>
                      <Typography.Text type="secondary">EmitterId: {selectedIssuer.emitterId}</Typography.Text>
                      <Typography.Text type="secondary">
                        {selectedIssuer.hasLogo ? "Текущий логотип будет заменён" : "Логотип ещё не добавлен"}
                      </Typography.Text>
                    </div>
                  </div>
                )}
                <div className={styles.actions}>
                  <Upload
                    accept=".png,.svg,image/png,image/svg+xml"
                    maxCount={1}
                    fileList={fileList}
                    beforeUpload={() => false}
                    onChange={info => setFileList(info.fileList.slice(-1))}
                  >
                    <Button disabled={!selectedIssuer}>Выбрать PNG или SVG</Button>
                  </Upload>
                  <Button type="primary" loading={saving} disabled={!selectedIssuer || !file} onClick={() => void upload()}>
                    Загрузить логотип
                  </Button>
                </div>
                <Typography.Text type="secondary">Максимальный размер файла — 2 МБ.</Typography.Text>
              </div>}
      </Card>
    </main>
  );
}
