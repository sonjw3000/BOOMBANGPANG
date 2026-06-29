from dataclasses import dataclass
from config import *
import re
from pathlib import Path


TASK_COMMIT_PATTERN = re.compile(
	r"^(TASK-\d{4})\s+(.+)$",
	re.IGNORECASE
)


@dataclass(frozen=True)
class TaskCommit:
	task_id: str
	task_name: str
	message: str

FRONTMATTER_PATTERN = re.compile(
	r"\A---\r?\n(.*?)\r?\n---(?:\r?\n|\Z)",
	re.DOTALL
)

def get_frontmatter(content: str):
	match = FRONTMATTER_PATTERN.match(content)

	if match is None:
		return None

	return match.group(1)

def find_task_file(task_id: str):
	if not TASK_PATH.exists():
		raise FileNotFoundError(f"Task 폴더를 찾을 수 없습니다: {TASK_PATH}")

	task_id_pattern = re.compile(
		rf"^task_id:\s*[\"']?"
		rf"{re.escape(task_id)}"
		rf"[\"']?\s*$",
		re.MULTILINE | re.IGNORECASE
	)

	found_file: Path | None = None

	for file_path in TASK_PATH.rglob("*.md"):
		content = file_path.read_text(encoding="utf-8")
		frontmatter = get_frontmatter(content)

		if frontmatter is None:
			continue

		if task_id_pattern.search(frontmatter) is None:
			continue

		if found_file is not None:
			raise RuntimeError(
				f"중복된 task_id입니다: {task_id}\n"
				f"- {found_file}\n"
				f"- {file_path}"
			)
		
		found_file = file_path

	return found_file

def parse_task_commit(commit_message: str):
	match = TASK_COMMIT_PATTERN.match(
		commit_message.strip()
	)

	if match is None:
		return None

	task_id = match.group(1).upper()
	message = match.group(2).strip()
	task_name=find_task_file(task_id)
	if not message:
		return None

	return TaskCommit(
		task_id=task_id,
		task_name=task_name,
		message=message
	)


def collect_task_commits(commits: list[dict]) -> list[TaskCommit]:
	task_commits: list[TaskCommit] = []

	print(commits)

	for commit in commits:
		parsed = parse_task_commit(commit["message"])

		# TASK-0001 형식이 아닌 커밋은 무시
		if parsed is None:
			continue

		task_commits.append(parsed)

	return task_commits


def build_daily_log_markdown(task_commits: list[TaskCommit]) -> str:
	lines = [
		"## 작업 기록",
		""
	]

	for commit in task_commits:
		lines.append(f"- [[{commit.task_name.stem}|{commit.task_id}]] {commit.message}")

	return "\n".join(lines).rstrip() + "\n"

