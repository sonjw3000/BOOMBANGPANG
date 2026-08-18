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

def find_task(
	notion: NotionClient,
	task_db: str,
	task_id: int,) -> dict | None:
	results = notion.query_data_source(
		task_db,
		{
			"property": "Task ID",
			"unique_id": {
				"equals": task_id,
			},
		},
	)

	if not results:
		return None

	if len(results) > 1:
		raise RuntimeError(
			f"Duplicate Task ID: {task_id}"
		)

	return results[0]


def main():
	token = os.environ["NOTION_TOKEN"]
	task_db = os.environ["NOTION_TASK_DB"]

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

		task_page = find_task(
			notion,
			task_db,
			task_id,
		)

		if task_page is None:
			print(
				f"  -> WARNING: "
				f"TASK-{task_id:04d} not found in Notion"
			)
			continue

		print(
			f"  -> Notion Task found: "
			f"{task_page['id']}"
		)


if __name__ == "__main__":
	main()
	