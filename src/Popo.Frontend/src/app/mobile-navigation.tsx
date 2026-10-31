"use client";

import {
  BarChartOutlined,
  DatabaseOutlined,
  SwapOutlined,
  UnorderedListOutlined,
  WalletOutlined
} from "@ant-design/icons";
import { TabBar } from "antd-mobile";
import { usePathname, useRouter } from "next/navigation";
import { useLayoutEffect } from "react";

const navigationItems = [
  { key: "/portfolio", label: "Портфель", icon: WalletOutlined },
  { key: "/positions", label: "Позиции", icon: UnorderedListOutlined },
  { key: "/trades", label: "Сделки", icon: SwapOutlined },
  { key: "/liquidity", label: "Ликвидность", icon: DatabaseOutlined },
  { key: "/recommendations", label: "Рекомендации", icon: BarChartOutlined }
];

export function MobileNavigation() {
  const pathname = usePathname();
  const router = useRouter();
  const selectedKey = navigationItems.find(({ key }) => pathname === key || pathname.startsWith(`${key}/`))?.key ?? "/portfolio";

  useLayoutEffect(() => {
    const viewport = window.visualViewport;
    if (!viewport) return;

    const updateViewportPosition = () => {
      const navigation = document.querySelector<HTMLElement>(".mobile-bottom-navigation");
      if (!navigation) return;

      document.documentElement.style.setProperty(
        "--mobile-viewport-top",
        `${viewport.offsetTop + viewport.height - navigation.getBoundingClientRect().height}px`
      );
      document.documentElement.style.setProperty("--mobile-viewport-bottom", "auto");
      document.documentElement.style.setProperty("--mobile-viewport-left-offset", `${viewport.offsetLeft}px`);
      document.documentElement.style.setProperty("--mobile-viewport-width", `${viewport.width}px`);
    };

    updateViewportPosition();
    let secondFrame = 0;
    const firstFrame = window.requestAnimationFrame(() => {
      updateViewportPosition();
      secondFrame = window.requestAnimationFrame(updateViewportPosition);
    });
    viewport.addEventListener("resize", updateViewportPosition);
    viewport.addEventListener("scroll", updateViewportPosition);
    window.addEventListener("resize", updateViewportPosition);

    return () => {
      window.cancelAnimationFrame(firstFrame);
      window.cancelAnimationFrame(secondFrame);
      viewport.removeEventListener("resize", updateViewportPosition);
      viewport.removeEventListener("scroll", updateViewportPosition);
      window.removeEventListener("resize", updateViewportPosition);
    };
  }, []);

  return (
    <nav className="mobile-bottom-navigation" aria-label="Основная навигация">
      <TabBar activeKey={selectedKey} safeArea>
        {navigationItems.map(({ key, label, icon: Icon }) => (
          <TabBar.Item
            key={key}
            icon={<Icon aria-hidden="true" />}
            title={label}
            aria-label={label}
            aria-current={selectedKey === key ? "page" : undefined}
            onClick={() => router.push(key)}
          />
        ))}
      </TabBar>
    </nav>
  );
}
