from dataclasses import dataclass
from datetime import datetime
import subprocess


ZERO_SHA = "0" * 40


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


def get_commits(before_sha: str, after_sha: str) -> list[Commit]:
	format_string = "%H%x1f%aI%x1f%s"

	if before_sha == ZERO_SHA:
		revision = after_sha
		extra_args = ["-1"]
	else:
		revision = f"{before_sha}..{after_sha}"
		extra_args = []

	output = _run_git(
		"log",
		"--reverse",
		*extra_args,
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