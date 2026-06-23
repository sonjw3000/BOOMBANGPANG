import git_log_parser as GP
import obsidian_keywords as OK
import log_2_obsidian as LOG
from config import *
from pathlib import Path
from datetime import date


def save_daily_note(markdown: str) -> Path:
	DAILY_PATH.mkdir(
		parents=True,
		exist_ok=True
	)

	today = date.today()

	file_name = today.strftime(
		"%Y-%m-%d.md"
	)

	file_path = DAILY_PATH / file_name

	file_path.write_text(
		markdown,
		encoding="utf-8"
	)

	return file_path

def main():
	commits = GP.get_today_commits()

	keywords = OK.build_keywords_from_docs()

	markdown = LOG.build_daily_log_markdown(
		commits,
		keywords
	)

	file_path = save_daily_note(
		markdown
	)

	print(
		f"Saved : {file_path}"
	)

if __name__ == "__main__":
	main()

