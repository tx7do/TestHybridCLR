#!/usr/bin/env python3
"""
热更新本地测试服务器。

同时 serve 代码热更新和资源热更新(Addressables)的内容：

  http://localhost:8080/
    ├── manifest.json          ← 代码热更版本清单
    ├── HotUpdate.dll          ← 代码热更 DLL
    └── Addressables/
        └── [BuildTarget]/     ← Addressables 远程构建产物
            ├── catalog_*.json
            ├── catalog_*.hash
            └── *.bundle

用法:
    cd <项目根目录>
    python3 tools/serve_hotupdate.py            # 默认端口 8080
    python3 tools/serve_hotupdate.py 9000       # 指定端口

运行时客户端默认连接 http://localhost:8080。
Addressables Profile 的 Remote.LoadPath 应配置为:
    http://localhost:8080/Addressables/[BuildTarget]
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
        print("请先在 Unity 中执行:")
        print("  1. HotUpdate/1. Build HotUpdate Package (代码打包)")
        print("  2. HotUpdate/Addressables/2. Build Addressables (资源打包)")
        sys.exit(1)

    os.chdir(serve_dir)

    # 添加 CORS 头，方便 UnityWebRequest / WebGL 跨域访问
    class CORSHandler(SimpleHTTPRequestHandler):
        def end_headers(self):
            self.send_header("Access-Control-Allow-Origin", "*")
            self.send_header("Access-Control-Allow-Methods", "GET, HEAD, OPTIONS")
            self.send_header("Access-Control-Allow-Headers", "*")
            super().end_headers()

        def log_message(self, format, *args):
            msg = format % args
            print(f"  \033[90m[{self.log_date_time_string()}]\033[0m {msg}")

    handler = functools.partial(CORSHandler, directory=serve_dir)

    with socketserver.TCPServer(("0.0.0.0", port), handler) as httpd:
        local_ip = get_local_ip()
        print("=" * 65)
        print("  HybridCLR 热更新本地服务器")
        print("=" * 65)
        print(f"  服务目录: {serve_dir}")
        print(f"  访问地址:")
        print(f"    本机:   http://localhost:{port}")
        print(f"    本机IP: http://{local_ip}:{port}  (真机测试用)")
        print()

        # 列出代码热更文件
        print("  ── 代码热更新 ──")
        list_files(serve_dir, prefix="    ")

        # 列出 Addressables 资源
        addr_dir = os.path.join(serve_dir, "Addressables")
        if os.path.isdir(addr_dir):
            print()
            print("  ── 资源热更新 (Addressables) ──")
            for subdir in sorted(os.listdir(addr_dir)):
                subdir_path = os.path.join(addr_dir, subdir)
                if os.path.isdir(subdir_path):
                    print(f"    /Addressables/{subdir}/")
                    list_files(subdir_path, prefix="      ",
                               url_prefix=f"/Addressables/{subdir}")
        else:
            print()
            print("  ── 资源热更新 ──")
            print("    (未找到 Addressables 目录，请执行资源打包)")

        print()
        print("  按 Ctrl+C 停止")
        print("=" * 65)

        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\n服务器已停止")
        finally:
            httpd.server_close()


def list_files(directory, prefix="  ", url_prefix=""):
    """列出目录下的文件（非递归，仅一层）。"""
    try:
        for name in sorted(os.listdir(directory)):
            filepath = os.path.join(directory, name)
            if os.path.isfile(filepath):
                size_kb = os.path.getsize(filepath) / 1024
                url = f"{url_prefix}/{name}" if url_prefix else f"/{name}"
                print(f"{prefix}{url}  ({size_kb:.1f} KB)")
    except OSError:
        pass


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
