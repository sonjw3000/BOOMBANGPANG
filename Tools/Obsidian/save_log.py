import git_log_parser as GP
import log_2_obsidian as LOG
import task_log_writer as TASK
import change_status as CS

from config import DAILY_PATH
from pathlib import Path
from datetime import date


def save_daily_note(markdown: str) -> Path:
	DAILY_PATH.mkdir(parents=True, exist_ok=True)

	file_name = date.today().strftime("%Y-%m-%d.md")

	file_path = DAILY_PATH / file_name
	file_path.write_text(markdown, encoding="utf-8")

	return file_path


def main() -> None:
	commits = GP.get_today_commits()
	task_commits = LOG.collect_task_commits(commits)

	if not task_commits:
		print("오늘 기록할 Task 커밋이 없습니다.")
		return

	updated_count = TASK.append_task_commits(task_commits)
	markdown = LOG.build_daily_log_markdown(task_commits)

	file_path = save_daily_note(markdown)

	for task in task_commits:
		CS.change_status_to_in_progress(task.task_name)

	print(f"Daily note saved: {file_path}")
	print(f"Task documents updated: {updated_count}")


if __name__ == "__main__":
	main()