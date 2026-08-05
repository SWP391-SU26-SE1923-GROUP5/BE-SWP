# Chat RAG Precise Source Pages Design

## Problem

For exhaustive questions, `RagContextExpander` may load every content chunk from the selected document so the LLM can produce a complete answer. `RagLocationFormatter` currently treats every supplied context chunk as supporting evidence. As a result, a correct answer based on pages 36-39 can be labeled as pages 1-53.

Verified reproduction:

- Question: `liệt kê các business rules trong tài liệu`
- Retrieval: 59 valid context chunks spanning the document
- Actual Business Rules section: PDF pages 36-39
- Current displayed location: pages 1-53

## Scope

Change only the Chat RAG document question-answering flow. Preserve the current HTTP request and response contracts, exhaustive retrieval behavior, answer format, document access controls, and upload/indexing flow.

## Design

### Internal source attribution

Assign a stable request-local source ID to every `RagContextSource` included in the prompt. Add the source ID beside the existing file name and page metadata. Instruct the LLM to finish its answer with one machine-readable attribution line containing only the IDs of contexts that directly support the answer.

The attribution line is internal protocol data. It must be removed before returning or persisting the answer.

### Parsing and validation

Parse only a strictly formatted attribution line at the end of the generated response. Accept only source IDs that were issued for the current request, deduplicate them, and preserve their prompt order. Ignore unknown, duplicated, or malformed IDs.

Content inside a document cannot create a valid citation merely by containing marker-like text: only the final protocol line is parsed, and every ID must match the request-local source map.

### Location formatting

Pass only validated supporting contexts to `RagLocationFormatter`. Continue grouping by document and merging consecutive pages, so the verified example renders `trang 36-39` rather than `trang 1-53`.

If the model omits or corrupts the attribution line, do not fall back to citing every retrieved page. Return the cleaned answer with the existing document name and an unknown-page location. This favors an explicit unknown location over a false location.

The deterministic yes/no shortcut does not use LLM attribution. It continues using the ranked contexts from which it derives its answer.

## Data Flow

1. Retrieve and select contexts exactly as today.
2. Build the prompt with request-local source IDs.
3. Generate the answer and internal source-ID line in one LLM call.
4. Strip and validate the internal line.
5. Format locations from validated supporting contexts only.
6. Return the same public response DTO and fields as today.

## Testing

- A regression test reproduces an exhaustive answer whose input contexts cover pages 1-53 but whose declared supporting contexts cover pages 36-39; the output must cite only pages 36-39.
- Parser tests cover valid IDs, duplicates, unknown IDs, malformed/missing attribution, and marker-like document content.
- Contract tests confirm no internal source IDs appear in the returned answer and no DTO shape changes.
- Existing non-exhaustive and deterministic yes/no behavior remains covered.

## Non-Goals

- Changing retrieval ranking, chunking, reindexing, or document ingestion
- Adding database citation entities or changing API DTOs
- Rewriting answer generation or guardrails unrelated to source locations
