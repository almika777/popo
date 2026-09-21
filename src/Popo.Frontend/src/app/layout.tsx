import type { Metadata } from "next";
import { headers } from "next/headers";
import { AntdRegistry } from "@ant-design/nextjs-registry";
import { AntdProvider } from "./antd-provider";
import { AppNavigation } from "./app-navigation";
import { InitializationStatusBanner } from "./initialization-status";
import { buildLanUrl } from "../lib/lan-url";
import "@fontsource-variable/jetbrains-mono";
import "./globals.css";

export const metadata: Metadata = {
  title: "Popo",
  description: "Личный интерфейс для работы с данными облигационного рынка"
};

export const dynamic = "force-dynamic";

export default async function RootLayout({
  children
}: Readonly<{
  children: React.ReactNode;
}>) {
  const requestHost = (await headers()).get("host")?.split(":")[0];
  const lanUrl = buildLanUrl(requestHost) ?? buildLanUrl(process.env.POPO_LAN_HOST);

  return (
    <html lang="ru">
      <body>
        <AntdRegistry>
          <AntdProvider>
            <header className="app-header">
              <h2 className="app-title">Popo</h2>
              <AppNavigation lanUrl={lanUrl} />
            </header>
            <InitializationStatusBanner />
            {children}
          </AntdProvider>
        </AntdRegistry>
      </body>
    </html>
  );
}
