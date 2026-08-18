import os
import re
from zoneinfo import ZoneInfo

from git import Commit, get_commits
from notion import NotionClient


TASK_PATTERN = re.compile(
	r"^TASK-(\d+)\s+(.+)$",
	re.IGNORECASE,
)

KST = ZoneInfo("Asia/Seoul")


def parse_task_commit(commit: Commit) -> tuple[int, str] | None:
	match = TASK_PATTERN.match(commit.message)

	if not match:
		return None

	task_id = int(match.group(1))
	description = match.group(2).strip()

	return task_id, description


def main():
	token = os.environ["NOTION_TOKEN"]

	task_db = os.environ["NOTION_TASK_DB"]
	worklog_db = os.environ["NOTION_WORKLOG_DB"]
	journal_db = os.environ["NOTION_JOURNAL_DB"]

	before_sha = os.environ["BEFORE_SHA"]
	after_sha = os.environ["AFTER_SHA"]

	notion = NotionClient(token)

	commits = get_commits(
		before_sha,
		after_sha,
	)

	print(f"{len(commits)} commits found")

	for commit in commits:
		date_kst = commit.date.astimezone(KST)

		print(
			date_kst.strftime("%Y-%m-%d %H:%M"),
			commit.sha[:7],
			commit.message,
		)

		task_commit = parse_task_commit(commit)

		if task_commit is None:
			print("  -> journal only")
			continue

		task_id, description = task_commit

		print(
			f"  -> TASK-{task_id:04d}"
			f" / {description}"
		)


if __name__ == "__main__":
	main()
	