#!/usr/bin/env python3
"""
热更新本地测试服务器。

serve HybridCLRData/ServerOutput/ 目录，
提供 manifest.json 和 HotUpdate.dll 的 HTTP 下载。

用法:
    cd <项目根目录>
    python3 tools/serve_hotupdate.py            # 默认端口 8080
    python3 tools/serve_hotupdate.py 9000       # 指定端口

运行时客户端默认连接 http://localhost:8080。
"""

import sys
import os
import functools
import socketserver
from http.server import SimpleHTTPRequestHandler


def main():
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8080

    # 服务器输出目录：项目根/HybridCLRData/ServerOutput/
    script_dir = os.path.dirname(os.path.abspath(__file__))
    project_root = os.path.dirname(script_dir)
    serve_dir = os.path.join(project_root, "HybridCLRData", "ServerOutput")

    if not os.path.isdir(serve_dir):
        print(f"[ERROR] 服务器输出目录不存在: {serve_dir}")
        print("请先在 Unity 中执行菜单 HotUpdate/1. Build HotUpdate Package (一键打包)")
        sys.exit(1)

    os.chdir(serve_dir)

    # 添加 CORS 头，方便 UnityWebRequest 跨域（本地无害）
    class CORSHandler(SimpleHTTPRequestHandler):
        def end_headers(self):
            self.send_header("Access-Control-Allow-Origin", "*")
            self.send_header("Access-Control-Allow-Methods", "GET, HEAD, OPTIONS")
            self.send_header("Access-Control-Allow-Headers", "*")
            super().end_headers()

        def log_message(self, format, *args):
            # 带颜色的时间戳前缀
            msg = format % args
            print(f"  \033[90m[{self.log_date_time_string()}]\033[0m {msg}")

    handler = functools.partial(CORSHandler, directory=serve_dir)

    with socketserver.TCPServer(("0.0.0.0", port), handler) as httpd:
        # 获取本机所有 IP 地址，方便真机连接
        print("=" * 60)
        print("  HybridCLR 热更新本地服务器")
        print("=" * 60)
        print(f"  服务目录: {serve_dir}")
        print(f"  访问地址:")
        print(f"    本机:   http://localhost:{port}")
        print(f"    本机IP: http://{get_local_ip()}:{port}  (真机测试用)")
        print()
        print("  可用文件:")
        for name in sorted(os.listdir(serve_dir)):
            filepath = os.path.join(serve_dir, name)
            if os.path.isfile(filepath):
                size_kb = os.path.getsize(filepath) / 1024
                print(f"    /{name}  ({size_kb:.1f} KB)")
        print()
        print("  按 Ctrl+C 停止")
        print("=" * 60)

        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\n服务器已停止")
        finally:
            httpd.server_close()


def get_local_ip():
    """获取本机局域网 IP 地址。"""
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("8.8.8.8", 80))
        ip = s.getsockname()[0]
        s.close()
        return ip
    except Exception:
        return "127.0.0.1"


if __name__ == "__main__":
    main()
