import os
import re

from enum import Enum, auto
from zoneinfo import ZoneInfo

from git import Commit, get_commits
from notion import NotionClient


# ============================================================
# Constants
# ============================================================

KST = ZoneInfo("Asia/Seoul")

STATUS_TODO = "시작 전"
STATUS_PROGRESS = "진행 중"
STATUS_COMPLETE = "완료"


class CommitType(Enum):
	TASK = auto()
	COMPLETE = auto()
	NORMAL = auto()


TASK_ALIASES = {
	"task",
}

COMPLETE_ALIASES = {
	"complete",
	"comp",
	"end",
}


COMMAND_PATTERN = re.compile(
	r"^(?P<command>[A-Za-z]+)[\s_-]*(?P<id>\d+)(?:\s+(.+))?$",
	re.IGNORECASE,
)


# ============================================================
# Environment
# ============================================================

def require_env(name: str) -> str:
	value = os.environ.get(name)

	if not value:
		raise RuntimeError(
			f"Required environment variable is missing: {name}"
		)

	return value


# ============================================================
# Commit parsing
# ============================================================

def parse_commit_message(
	message: str,
) -> tuple[CommitType, int | None, str]:

	match = COMMAND_PATTERN.match(message.strip())

	if not match:
		return CommitType.NORMAL, None, message.strip()

	command = match.group("command").lower()
	task_id = int(match.group("id"))
	description = (match.group(3) or "").strip()

	if command in TASK_ALIASES:
		return CommitType.TASK, task_id, description

	if command in COMPLETE_ALIASES:
		return CommitType.COMPLETE, task_id, description

	return CommitType.NORMAL, None, message.strip()


def make_commit_url(commit: Commit) -> str:
	repository = require_env("GITHUB_REPOSITORY")

	return (
		f"https://github.com/"
		f"{repository}/commit/{commit.sha}"
	)


# ============================================================
# Notion property helpers
# ============================================================

def relation(page_id: str) -> dict:
	return {
		"relation": [
			{
				"id": page_id,
			}
		]
	}


def status(value: str) -> dict:
	return {
		"status": {
			"name": value,
		}
	}


def get_status(page: dict) -> str | None:
	prop = page["properties"].get("Status")

	if not prop:
		return None

	value = prop.get("status")

	if not value:
		return None

	return value["name"]


# ============================================================
# Task
# ============================================================

def find_task(
	notion: NotionClient,
	task_db: str,
	task_id: int,
) -> dict | None:

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


def set_task_status(
	notion: NotionClient,
	task_page: dict,
	new_status: str,
) -> None:

	current = get_status(task_page)

	if current == new_status:
		return

	notion.update_page(
		task_page["id"],
		{
			"Status": status(new_status),
		},
	)

	print(
		f"  -> Task status: "
		f"{current} -> {new_status}"
	)


# ============================================================
# Epic
# ============================================================

def ensure_epic_started(
	notion: NotionClient,
	task_page: dict,
) -> None:

	epic_property = task_page["properties"].get("Epic")

	if not epic_property:
		return

	relations = epic_property.get("relation", [])

	if not relations:
		return

	for item in relations:
		epic_page = notion.get_page(item["id"])

		if get_status(epic_page) != STATUS_TODO:
			continue

		notion.update_page(
			epic_page["id"],
			{
				"Status": status(STATUS_PROGRESS),
			},
		)

		print(
			f"  -> Epic started: {epic_page['id']}"
		)


# ============================================================
# Work Log
# ============================================================

def find_worklog(
	notion: NotionClient,
	worklog_db: str,
	commit_url: str,
) -> dict | None:

	return notion.query_first(
		worklog_db,
		{
			"property": "Commit URL",
			"url": {
				"equals": commit_url,
			},
		},
	)


def create_worklog(
	notion: NotionClient,
	worklog_db: str,
	task_page: dict,
	description: str,
	date: str,
	commit_url: str,
) -> dict:

	return notion.create_page(
		worklog_db,
		{
			"Description": {
				"title": [
					{
						"text": {
							"content": description,
						}
					}
				]
			},

			"Date": {
				"date": {
					"start": date,
				}
			},

			"Task": relation(
				task_page["id"]
			),

			"Commit URL": {
				"url": commit_url,
			},
		},
	)


# ============================================================
# Journal
# ============================================================

def find_journal(
	notion: NotionClient,
	journal_db: str,
	date: str,
) -> dict | None:

	return notion.query_first(
		journal_db,
		{
			"property": "Date",
			"date": {
				"equals": date,
			},
		},
	)


def create_journal(
	notion: NotionClient,
	journal_db: str,
	date: str,
) -> dict:

	return notion.create_page(
		journal_db,
		{
			"이름": {
				"title": [
					{
						"text": {
							"content": date,
						}
					}
				]
			},

			"Date": {
				"date": {
					"start": date,
				}
			},
		},
		[
			{
				"object": "block",
				"type": "heading_2",
				"heading_2": {
					"rich_text": [
						{
							"type": "text",
							"text": {
								"content": "작업 기록",
							},
						}
					]
				},
			}
		],
	)


def add_relation(
	notion: NotionClient,
	page: dict,
	property_name: str,
	target_page_id: str,
) -> None:

	prop = page["properties"][property_name]
	current = prop.get("relation", [])

	ids = [
		item["id"]
		for item in current
	]

	if target_page_id in ids:
		return

	ids.append(target_page_id)

	notion.update_page(
		page["id"],
		{
			property_name: {
				"relation": [
					{
						"id": page_id,
					}
					for page_id in ids
				]
			}
		},
	)


# ============================================================
# Journal blocks
# ============================================================

def make_normal_log_block(
	message: str,
) -> dict:

	return {
		"object": "block",
		"type": "bulleted_list_item",
		"bulleted_list_item": {
			"rich_text": [
				{
					"type": "text",
					"text": {
						"content": message,
					},
				}
			]
		},
	}


def make_task_log_block(
	task_id: int,
	task_page: dict,
	description: str,
	worklog_page: dict,
) -> dict:

	return {
		"object": "block",
		"type": "bulleted_list_item",
		"bulleted_list_item": {
			"rich_text": [
				{
					"type": "text",
					"text": {
						"content": f"TASK-{task_id:04d}",
						"link": {
							"url": task_page["url"],
						},
					},
				},

				{
					"type": "text",
					"text": {
						"content": " ",
					},
				},

				{
					"type": "text",
					"text": {
						"content": description,
						"link": {
							"url": worklog_page["url"],
						},
					},
				},
			]
		},
	}

def create_task_comment(
	notion: NotionClient,
	task_page: dict,
	commit: Commit,
	description: str,
	command: CommitType,
) -> None:

	commit_url = make_commit_url(commit)

	if command == CommitType.COMPLETE:
		prefix = "✓ 완료"
	else:
		prefix = "● 작업"

	notion.create_comment(
		task_page["id"],
		[
			{
				"type": "text",
				"text": {
					"content": f"{prefix} · {description} · ",
				},
			},
			{
				"type": "text",
				"text": {
					"content": commit.sha[:7],
					"link": {
						"url": commit_url,
					},
				},
			},
		],
	)

# ============================================================
# Main
# ============================================================

def main():
	token = require_env("NOTION_TOKEN")

	task_db = require_env("NOTION_TASK_DB")
	worklog_db = require_env("NOTION_WORKLOG_DB")
	journal_db = require_env("NOTION_JOURNAL_DB")

	before_sha = require_env("BEFORE_SHA")
	after_sha = require_env("AFTER_SHA")

	notion = NotionClient(token)

	commits = get_commits(
		before_sha,
		after_sha,
	)

	print(f"{len(commits)} commits found")

	for commit in commits:
		date_kst = commit.date.astimezone(KST)
		date = date_kst.strftime("%Y-%m-%d")

		print(
			date_kst.strftime("%Y-%m-%d %H:%M"),
			commit.sha[:7],
			commit.message,
		)

		command, task_id, description = (
			parse_commit_message(commit.message)
		)

		# ----------------------------------------------------
		# Journal
		# ----------------------------------------------------

		journal = find_journal(
			notion,
			journal_db,
			date,
		)

		if journal is None:
			journal = create_journal(
				notion,
				journal_db,
				date,
			)

			print(
				f"  -> Journal created: {date}"
			)

		# ----------------------------------------------------
		# Normal commit
		# ----------------------------------------------------

		if command == CommitType.NORMAL:
			notion.append_blocks(
				journal["id"],
				[
					make_normal_log_block(
						commit.message
					)
				],
			)

			print("  -> Journal only")
			continue

		assert task_id is not None

		# ----------------------------------------------------
		# Find Task
		# ----------------------------------------------------

		task_page = find_task(
			notion,
			task_db,
			task_id,
		)

		if task_page is None:
			print(
				f"  -> WARNING: "
				f"TASK-{task_id:04d} not found"
			)

			# Task를 못 찾아도 일지에는 원문을 남긴다.
			notion.append_blocks(
				journal["id"],
				[
					make_normal_log_block(
						commit.message
					)
				],
			)

			continue

		print(
			f"  -> Task found: TASK-{task_id:04d}"
		)

		# ----------------------------------------------------
		# Task Status
		# ----------------------------------------------------

		if command == CommitType.COMPLETE:
			set_task_status(
				notion,
				task_page,
				STATUS_COMPLETE,
			)

		else:
			set_task_status(
				notion,
				task_page,
				STATUS_PROGRESS,
			)

		# ----------------------------------------------------
		# Epic Status
		# ----------------------------------------------------

		ensure_epic_started(
			notion,
			task_page,
		)

		# ----------------------------------------------------
		# Work Log
		# ----------------------------------------------------

		commit_url = make_commit_url(commit)

		worklog = find_worklog(
			notion,
			worklog_db,
			commit_url,
		)

		if worklog is None:
			worklog = create_worklog(
				notion,
				worklog_db,
				task_page,
				description,
				date,
				commit_url,
			)

			print(
				"  -> Work Log created"
			)

			create_task_comment(
				notion,
				task_page,
				commit,
				description,
				command,
			)

			print(
				"  -> Task comment created"
			)

		else:
			print(
				"  -> Work Log already exists"
			)

		# ----------------------------------------------------
		# Journal relations
		# ----------------------------------------------------

		add_relation(
			notion,
			journal,
			"Tasks",
			task_page["id"],
		)

		# 첫 PATCH 이후 최신 Relation 상태 다시 획득
		journal = notion.get_page(
			journal["id"]
		)

		add_relation(
			notion,
			journal,
			"Work Logs",
			worklog["id"],
		)

		# ----------------------------------------------------
		# Journal content
		# ----------------------------------------------------

		notion.append_blocks(
			journal["id"],
			[
				make_task_log_block(
					task_id,
					task_page,
					description,
					worklog,
				)
			],
		)

		print("  -> Journal updated")


if __name__ == "__main__":
	main()
