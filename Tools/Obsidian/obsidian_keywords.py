from pathlib import Path
from dotenv import load_dotenv
import os

load_dotenv()

VAULT_PATH = Path(
    os.getenv("OBSIDIAN_VAULT_PATH")
)

DOCS_PATH = VAULT_PATH / "Docs"

def build_keywords_from_docs() -> list[str]:
	docs_path = Path(DOCS_PATH)

	if not docs_path.exists():
		raise FileNotFoundError(f"Docs folder not found: {DOCS_PATH}")

	keywords = []

	for md_file in docs_path.rglob("*.md"):
		keyword = md_file.stem.strip()

		if keyword:
			keywords.append(keyword)

	# 긴 단어 먼저 치환해야 ZoneFilter가 Zone보다 먼저 잡힘
	keywords = sorted(set(keywords), key=len, reverse=True)

	return keywords

def main():
	keywords = build_keywords_from_docs()

	print(keywords)

if __name__ == "__main__":
	main()


