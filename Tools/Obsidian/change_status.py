import re
from pathlib import Path


def change_status_to_in_progress(file_path: Path) -> bool:
	content = file_path.read_text(
		encoding="utf-8"
	)

	pattern = re.compile(
		r"^(status:\s*)todo\s*$",
		re.MULTILINE | re.IGNORECASE
	)

	updated_content, changed_count = pattern.subn(
		r"\1in-progress",
		content,
		count=1
	)

	if changed_count == 0:
		return False

	file_path.write_text(
		updated_content,
		encoding="utf-8"
	)

	return True