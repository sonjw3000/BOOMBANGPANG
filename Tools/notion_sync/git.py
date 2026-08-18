from dataclasses import dataclass
from datetime import datetime
import subprocess


@dataclass
class Commit:
	sha: str
	date: datetime
	message: str


def _run_git(*args: str) -> str:
	result = subprocess.run(
		["git", *args],
		stdout=subprocess.PIPE,
		stderr=subprocess.PIPE,
		text=True,
		encoding="utf-8",
		check=False,
	)

	if result.returncode != 0:
		raise RuntimeError(
			f"git command failed: git {' '.join(args)}\n"
			f"{result.stderr}"
		)

	return result.stdout.strip()


ZERO_SHA = "0" * 40


def get_commits(before_sha: str, after_sha: str) -> list[Commit]:
	format_string = "%H%x1f%aI%x1f%s"

	if before_sha == ZERO_SHA:
		# 새 브랜치 최초 push.
		# Dry run에서 전체 history를 처리하지 않도록 HEAD 하나만 확인.
		revision = after_sha
		max_count = ["-1"]
	else:
		revision = f"{before_sha}..{after_sha}"
		max_count = []

	output = _run_git(
		"log",
		"--reverse",
		*max_count,
		f"--pretty=format:{format_string}",
		revision,
	)

	if not output:
		return []

	commits: list[Commit] = []

	for line in output.splitlines():
		sha, date, message = line.split("\x1f", 2)

		commits.append(
			Commit(
				sha=sha,
				date=datetime.fromisoformat(date),
				message=message,
			)
		)

	return commits
