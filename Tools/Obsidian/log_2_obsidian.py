import git_log_parser as GP
import obsidian_keywords as OK
from collections import defaultdict
import re
import config as config

SECTION_ORDER = ["구현", "개선", "버그 수정", "기타"]

PREFIX_TO_SECTION = {
	"구현": "구현",
	"impl": "구현",

	"개선": "개선",
	"improve": "개선",

	"버그 수정": "버그 수정",
	"버그수정": "버그 수정",
	"버그": "버그 수정",
	"수정": "버그 수정",
	"fix": "버그 수정",
}



def parse_commit_message(message: str) -> tuple[str, str]:
	if ":" not in message:
		return "기타", message.strip()

	prefix, body = message.split(":", 1)

	prefix = prefix.strip()
	body = body.strip()

	section = PREFIX_TO_SECTION.get(prefix, "기타")

	if not body:
		body = message.strip()

	return section, body


def make_obsidian_links(text: str, keywords: list[str]) -> str:
	for keyword in keywords:
		pattern = re.compile(
			rf"(?<!\[\[){re.escape(keyword)}(?!\]\])",
			re.IGNORECASE
		)

		text = pattern.sub(
			f"[[{keyword}]]",
			text
		)

	return text


def group_commits_for_daily_log(commits, keywords) -> dict[str, list[str]]:
	keywords = keywords or []
	grouped = defaultdict(list)

	for commit in commits:
		section, body = parse_commit_message(commit["message"])

		body = make_obsidian_links(body, keywords)

		grouped[section].append(body)

	return grouped


def build_daily_log_markdown(commits: list[dict], keywords: list[str]) -> str:
	grouped = group_commits_for_daily_log(commits, keywords)

	lines = []

	for section in SECTION_ORDER:
		items = grouped.get(section, [])

		lines.append(f"## {section}")

		if items:
			for item in items:
				lines.append(f"- {item}")
		else:
			lines.append("- ")

		lines.append("")

	return "\n".join(lines).strip()


def main():
	commits = GP.get_today_commits()
	keywords = OK.build_keywords_from_docs()
	
	markdown = build_daily_log_markdown(commits, keywords)

	print(markdown)
	

if __name__ == "__main__":
	main()
