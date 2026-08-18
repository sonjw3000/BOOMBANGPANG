import json
import urllib.error
import urllib.request


NOTION_VERSION = "2026-03-11"


class NotionClient:
	def __init__(self, token: str):
		self.token = token

	def _request(
		self,
		method: str,
		path: str,
		body: dict | None = None,
	) -> dict:
		url = f"https://api.notion.com/v1{path}"

		data = None

		if body is not None:
			data = json.dumps(body).encode("utf-8")

		request = urllib.request.Request(
			url,
			data=data,
			method=method,
			headers={
				"Authorization": f"Bearer {self.token}",
				"Notion-Version": NOTION_VERSION,
				"Content-Type": "application/json",
			},
		)

		try:
			with urllib.request.urlopen(request) as response:
				return json.loads(response.read())

		except urllib.error.HTTPError as error:
			response = error.read().decode("utf-8")

			raise RuntimeError(
				f"Notion API failed\n"
				f"{method} {path}\n"
				f"HTTP {error.code}\n"
				f"{response}"
			) from error

	def query_data_source(
		self,
		data_source_id: str,
		filter_: dict | None = None,
	) -> list[dict]:
		body = {}

		if filter_ is not None:
			body["filter"] = filter_

		result = self._request(
			"POST",
			f"/data_sources/{data_source_id}/query",
			body,
		)

		return result["results"]

	def query_first(
		self,
		data_source_id: str,
		filter_: dict,
	) -> dict | None:
		results = self.query_data_source(
			data_source_id,
			filter_,
		)

		if not results:
			return None

		return results[0]

	def get_page(
		self,
		page_id: str,
	) -> dict:
		return self._request(
			"GET",
			f"/pages/{page_id}",
		)

	def create_page(
		self,
		data_source_id: str,
		properties: dict,
		children: list[dict] | None = None,
	) -> dict:
		body = {
			"parent": {
				"type": "data_source_id",
				"data_source_id": data_source_id,
			},
			"properties": properties,
		}

		if children:
			body["children"] = children

		return self._request(
			"POST",
			"/pages",
			body,
		)

	def update_page(
		self,
		page_id: str,
		properties: dict,
	) -> dict:
		return self._request(
			"PATCH",
			f"/pages/{page_id}",
			{
				"properties": properties,
			},
		)

	def append_blocks(
		self,
		page_id: str,
		children: list[dict],
	) -> None:
		self._request(
			"PATCH",
			f"/blocks/{page_id}/children",
			{
				"children": children,
			},
		)
		