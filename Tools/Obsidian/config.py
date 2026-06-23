from pathlib import Path
from dotenv import load_dotenv
import os

load_dotenv()

VAULT_PATH = Path(
	os.getenv("OBSIDIAN_VAULT_PATH")
).expanduser()

DOCS_PATH = VAULT_PATH / os.getenv(
	"OBSIDIAN_DOCS_PATH",
	"Docs"
)

DAILY_PATH = VAULT_PATH / os.getenv(
	"OBSIDIAN_DAILY_PATH",
	"Daily"
)

GIT_SINCE = os.getenv(
	"GIT_SINCE",
	"midnight"
)
