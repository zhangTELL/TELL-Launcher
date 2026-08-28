"""扫描 XAML：找出同一文件内 StaticResource 引用早于 x:Key 定义的情况。

WPF 的 StaticResource 在同一 ResourceDictionary 中要求"先定义后引用"，
顺序颠倒会在加载时抛"找不到资源"。这个错误编译期不报错，只有运行时才炸，
且若发生在 DataTemplate 里，每个实例化都会触发一次。

用法：python check_xaml_resource_order.py <项目根目录>
"""

import re
import sys
from pathlib import Path

KEY_PATTERN = re.compile(r'x:Key\s*=\s*"([^"]+)"')
STATIC_REF_PATTERN = re.compile(r'\{StaticResource\s+([A-Za-z_][\w.]*)\s*\}')


def check_file(path: Path) -> list[str]:
    lines = path.read_text(encoding="utf-8").splitlines()

    # 记录每个 key 首次定义的位置（行号从 1 开始）
    definitions: dict[str, int] = {}
    for index, line in enumerate(lines, start=1):
        for key in KEY_PATTERN.findall(line):
            definitions.setdefault(key, index)

    problems: list[str] = []
    for index, line in enumerate(lines, start=1):
        for reference in STATIC_REF_PATTERN.findall(line):
            defined_at = definitions.get(reference)
            if defined_at is not None and defined_at > index:
                problems.append(
                    f"{path}:{index} 引用 '{reference}'，"
                    f"但该资源在第 {defined_at} 行才定义"
                )

    return problems


def main() -> int:
    if len(sys.argv) < 2:
        print("用法: python check_xaml_resource_order.py <目录>")
        return 2

    root = Path(sys.argv[1])
    problems: list[str] = []
    scanned = 0

    for xaml_file in root.rglob("*.xaml"):
        if any(part in {"obj", "bin"} for part in xaml_file.parts):
            continue
        scanned += 1
        problems.extend(check_file(xaml_file))

    print(f"已扫描 {scanned} 个 XAML 文件")
    if problems:
        print(f"发现 {len(problems)} 处顺序问题：")
        for problem in problems:
            print(f"  {problem}")
        return 1

    print("未发现 StaticResource 顺序问题")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
