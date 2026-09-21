"use client";

import Link from "next/link";
import { Menu } from "antd";
import { usePathname } from "next/navigation";

const items = [
  { key: "/portfolio", label: <Link href="/portfolio">Портфель</Link> },
  { key: "/positions", label: <Link href="/positions">Позиции</Link> },
  { key: "/trades", label: <Link href="/trades">Сделки</Link> },
  { key: "/cash", label: <Link href="/cash">Ликвидность</Link> },
  { key: "/recommendations", label: <Link href="/recommendations">Рекомендации</Link> }
];

type AppNavigationProps = {
  lanUrl: string | null;
};

export function AppNavigation({ lanUrl }: AppNavigationProps) {
  const pathname = usePathname();
  const selectedKey = items.some((item) => pathname === item.key || pathname.startsWith(`${item.key}/`))
    ? items.find((item) => pathname === item.key || pathname.startsWith(`${item.key}/`))!.key
    : "/portfolio";

  return (
    <>
      <Menu mode="horizontal" theme="dark" selectedKeys={[selectedKey]} items={items} />
      {lanUrl ? (
        <a
          className="app-network-link"
          href={lanUrl}
          target="_blank"
          rel="noreferrer"
          title="Откройте эту ссылку на телефоне в той же Wi-Fi-сети"
        >
          {lanUrl.replace(/^https?:\/\//, "")}
        </a>
      ) : (
        <span className="app-network-link app-network-link-disabled" title="Перезапустите Popo в активной Wi-Fi-сети">
          Адрес недоступен
        </span>
      )}
    </>
  );
}
