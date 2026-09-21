"use client";

import { App as AntdApp, ConfigProvider, theme } from "antd";
import ruRU from "antd/locale/ru_RU";
import dayjs from "dayjs";
import "dayjs/locale/ru";

dayjs.locale("ru");

export function AntdProvider({ children }: { children: React.ReactNode }) {
  return (
    <ConfigProvider
      locale={ruRU}
      theme={{
        algorithm: theme.darkAlgorithm,
        token: {
          colorBgLayout: "#000000",
          fontFamily: "'JetBrains Mono Variable', monospace"
        }
      }}
    >
      <AntdApp>{children}</AntdApp>
    </ConfigProvider>
  );
}
