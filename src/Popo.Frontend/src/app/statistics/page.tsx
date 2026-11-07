"use client";

import { useEffect, useState } from "react";
import { Alert, Button, Card, DatePicker, Empty, InputNumber, Select, Spin, Tooltip, Typography } from "antd";
import Image from "next/image";
import dayjs, { type Dayjs } from "dayjs";
import styles from "./statistics.module.css";
import {
  bondDailyStatisticsApi,
  type BondDailyStatisticsResult,
  type BondIssuerCategory
} from "../../lib/bond-daily-statistics-api";
import {
  formatBondDailyTurnover,
  getBondDailyChangeDisplay,
  getDefaultBondStatisticsDate,
  isBondStatisticsDateDisabled
} from "../../lib/bond-daily-statistics-presentation";
import { issuerLogosApi } from "../../lib/issuer-logos-api";

function getMoscowToday(): Dayjs {
  const parts = new Intl.DateTimeFormat("en", {
    timeZone: "Europe/Moscow",
    year: "numeric",
    month: "2-digit",
    day: "2-digit"
  }).formatToParts(new Date());
  const value = Object.fromEntries(parts.map(part => [part.type, part.value]));
  return dayjs(`${value.year}-${value.month}-${value.day}`);
}

export default function StatisticsPage() {
  const today = getMoscowToday();
  const [date, setDate] = useState(() => getDefaultBondStatisticsDate(today));
  const [minimumTrades, setMinimumTrades] = useState(5);
  const [minimumTurnoverRub, setMinimumTurnoverRub] = useState(1_000_000);
  const [issuerCategory, setIssuerCategory] = useState<BondIssuerCategory | "All">("All");
  const [result, setResult] = useState<BondDailyStatisticsResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    setResult(null);
    void bondDailyStatisticsApi.getDailyStatistics(
      date.format("YYYY-MM-DD"),
      minimumTrades,
      minimumTurnoverRub,
      controller.signal,
      issuerCategory === "All" ? undefined : issuerCategory)
      .then(setResult)
      .catch(reason => {
        if (!controller.signal.aborted) {
          const errorMessage = reason instanceof Error ? reason.message : "Не удалось загрузить статистику.";
          setError(errorMessage);
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });

    return () => controller.abort();
  }, [date, minimumTrades, minimumTurnoverRub, issuerCategory]);

  const currentResult = result?.date === date.format("YYYY-MM-DD") ? result : null;

  return (
    <main className={`app-content ${styles.page}`}>
      <header className={styles.heading}>
        <div>
          <Typography.Title level={1}>Статистика</Typography.Title>
          <Typography.Text type="secondary">Топ-5 облигаций по изменению цены и обороту за день</Typography.Text>
        </div>
        <Button href="/statistics/issuer-logos">Логотипы эмитентов</Button>
      </header>

      <section className={styles.filterPanel} aria-label="Фильтры">
        <div className={styles.filters}>
          <label className={styles.filter}>
            <Typography.Text type="secondary">Дата</Typography.Text>
            <DatePicker
              value={date}
              format="DD.MM.YYYY"
              allowClear={false}
              disabledDate={candidate => isBondStatisticsDateDisabled(candidate, today)}
              onChange={value => value && setDate(value)}
              aria-label="Дата статистики"
            />
          </label>
          <label className={styles.filter}>
            <Typography.Text type="secondary">Мин. сделок за сессию</Typography.Text>
            <InputNumber
              min={0}
              step={1}
              precision={0}
              value={minimumTrades}
              onChange={value => setMinimumTrades(value ?? 0)}
              aria-label="Минимум сделок за сессию"
            />
          </label>
          <label className={styles.filter}>
            <Typography.Text type="secondary">Мин. оборот за сессию, ₽</Typography.Text>
            <InputNumber
              min={0}
              step={100_000}
              precision={0}
              value={minimumTurnoverRub}
              onChange={value => setMinimumTurnoverRub(value ?? 0)}
              aria-label="Минимальный оборот за сессию в рублях"
            />
          </label>
          <label className={styles.filter}>
            <Typography.Text type="secondary">Вид облигации</Typography.Text>
            <Select
              value={issuerCategory}
              onChange={setIssuerCategory}
              aria-label="Вид облигации"
              options={[
                { value: "All", label: "Все виды" },
                { value: "Government", label: "Государственные" },
                { value: "Municipal", label: "Муниципальные" },
                { value: "Corporate", label: "Корпоративные" }
              ]}
            />
          </label>
        </div>
      </section>

      {error && <Alert type="error" showIcon message="Не удалось загрузить статистику" description={error} />}

      <div className={styles.cards}>
        <Card className={styles.card} title="Снижение">
          <div className={styles.cardContent}>
            {loading && <div className={styles.loading}><Spin /></div>}
            {!loading && !error && currentResult && (
              currentResult.declines.length === 0
                ? <Empty description={currentResult.comparedWithDate ? "Облигаций со снижением цены нет" : "Недостаточно данных для сравнения торговых сессий"} />
                : <BondChangeList bonds={currentResult.declines} />
            )}
          </div>
        </Card>

        <Card className={styles.card} title="Рост">
          <div className={styles.cardContent}>
            {loading && <div className={styles.loading}><Spin /></div>}
            {!loading && !error && currentResult && (
              currentResult.increases.length === 0
                ? <Empty description={currentResult.comparedWithDate ? "Облигаций с ростом цены нет" : "Недостаточно данных для сравнения торговых сессий"} />
                : <BondChangeList bonds={currentResult.increases} />
            )}
          </div>
        </Card>

        <Card className={styles.card} title="Оборот">
          <div className={styles.cardContent}>
            {loading && <div className={styles.loading}><Spin /></div>}
            {!loading && !error && currentResult && (
              currentResult.turnovers.length === 0
                ? <Empty description="Нет данных по обороту" />
                : <BondTurnoverList bonds={currentResult.turnovers} />
            )}
          </div>
        </Card>
      </div>
    </main>
  );
}

function BondTurnoverList({ bonds }: { bonds: BondDailyStatisticsResult["turnovers"] }) {
  return (
    <ol className={styles.list}>
      {bonds.map((bond, index) => (
        <li className={styles.item} key={`${bond.secId}:${bond.boardId}`}>
          <BondRank emitterId={bond.emitterId} rank={index + 1} />
          <div className={styles.identity}>
            <Typography.Text strong>{bond.shortName || bond.secId}</Typography.Text>
            <Typography.Text type="secondary">{bond.secId}</Typography.Text>
          </div>
          <Typography.Text className={styles.turnover}>{formatBondDailyTurnover(bond.turnoverRub)}</Typography.Text>
        </li>
      ))}
    </ol>
  );
}

function BondChangeList({ bonds }: { bonds: BondDailyStatisticsResult["declines"] }) {
  return (
    <ol className={styles.list}>
      {bonds.map((bond, index) => {
        const display = getBondDailyChangeDisplay(bond);
        return (
          <li className={styles.item} key={`${bond.secId}:${bond.boardId}`}>
            <BondRank emitterId={bond.emitterId} rank={index + 1} />
            <div className={styles.identity}>
              <Typography.Text strong>{bond.shortName || bond.secId}</Typography.Text>
              <Typography.Text type="secondary">{bond.secId}</Typography.Text>
            </div>
            <div className={styles.change}>
              <Tooltip title={display.tooltip} trigger={["hover", "focus"]}>
                <Typography.Text className={display.isIncrease ? styles.increase : styles.decline} tabIndex={0}>
                  {display.percent}
                </Typography.Text>
              </Tooltip>
            </div>
          </li>
        );
      })}
    </ol>
  );
}

function BondRank({ emitterId, rank }: { emitterId: number | null; rank: number }) {
  const [imageFailed, setImageFailed] = useState(false);

  if (emitterId && !imageFailed) {
    return (
      <span className={styles.rankLogoBadge}>
        <Image
          className={styles.rankLogo}
          src={issuerLogosApi.imageUrl(emitterId)}
          alt=""
          aria-hidden="true"
          width={24}
          height={24}
          unoptimized
          onError={() => setImageFailed(true)}
        />
      </span>
    );
  }

  return <span className={styles.rank}>{rank}</span>;
}
