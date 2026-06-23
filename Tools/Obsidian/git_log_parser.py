import subprocess


def run_git_command(args: list[str]) -> str:
	result = subprocess.run(
		args,
		capture_output=True,
		text=True,
		check=True
	)
	return result.stdout.strip()


def get_today_commits() -> list[dict]:
	output = run_git_command([
		"git",
		"log",
		"--all",
		"--since=midnight",
		"--pretty=format:%h|%ad|%d|%s",
		"--date=format:%H:%M"
	])

	commits = []

	if not output:
		return commits

	for line in output.splitlines():
		commit_hash, commit_time, refs, message = line.split("|", 3)

		commits.append({
			"hash": commit_hash,
			"time": commit_time,
			"refs": refs.strip(),
			"message": message
		})

	return commits


def main():
	commits = get_today_commits()

	if not commits:
		print("오늘 커밋 없음")
		return

	for commit in commits:
		print(
			f"[{commit['time']}] "
			f"{commit['hash']} "
			# f"{commit['refs']} "
			f"{commit['message']}"
		)


if __name__ == "__main__":
	main()
