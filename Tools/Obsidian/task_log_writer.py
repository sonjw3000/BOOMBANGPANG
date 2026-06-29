from pathlib import Path
from config import *
import re

from config import (
	TASK_PATH,
	TASK_COMMIT_HEADING
)
from log_2_obsidian import TaskCommit


def append_message_under_heading(content: str, message: str) -> tuple[str, bool]:
	entry = f"- {message}"

	# post-commit이 다시 실행돼도 같은 줄을 중복 추가하지 않음
	if entry in content.splitlines():
		return content, False

	heading_pattern = re.compile(
		rf"^{re.escape(TASK_COMMIT_HEADING)}\s*$",
		re.MULTILINE
	)

	match = heading_pattern.search(content)

	if match is None:
		updated = (
			content.rstrip()
			+ "\n\n"
			+ TASK_COMMIT_HEADING
			+ "\n\n"
			+ entry
			+ "\n"
		)

		return updated, True

	insert_position = match.end()

	updated = (
		content[:insert_position]
		+ "\n\n"
		+ entry
		+ content[insert_position:]
	)

	return updated, True


def append_task_commit(task_commit: TaskCommit) -> bool:
	task_file = task_commit.task_name

	if task_file is None:
		print(
			f"Task 문서를 찾지 못했습니다: "
			f"{task_commit.task_id}"
		)

		return False

	content = task_file.read_text(encoding="utf-8")

	updated, changed = append_message_under_heading(content=content, message=task_commit.message)
	if not changed:
		return False

	task_file.write_text(updated, encoding="utf-8")

	print(
		f"Task updated: "
		f"{task_commit.task_id} -> {task_file}"
	)

	return True


def append_task_commits(task_commits: list[TaskCommit]) -> int:
	updated_count = 0

	for task_commit in task_commits:
		if append_task_commit(task_commit):
			updated_count += 1

	return updated_count
