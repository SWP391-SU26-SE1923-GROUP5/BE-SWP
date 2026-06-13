# Spec: Tích Hợp DocumentChunkingController Vào Luồng Chính

**Date:** 2026-06-13
**Status:** Approved

## 1. Mục Tiêu

Loại bỏ `DocumentChunkingController` và `ChunkingFile`, chỉ dùng `DocumentUploadController` + `RagChatService` làm hệ thống RAG duy nhất.

## 2. Background

Hiện tại có **hai luồng RAG riêng biệt**:

| | DocumentUploadController | DocumentChunkingController |
|---|---|---|
| Chunking | `DocumentProcessingService` (plain text) | `ChunkingFile` (JSON {text, index}) |
| Retrieval | RagChatService / VectorStoreService | In-memory Cosine Similarity |
| Storage | ChunkJson = plain text | ChunkJson = JSON string |

Điều này gây ra:
- Chunk format không tương thích
- Retrieval không nhất quán
- Code trùng lặp

## 3. Thiết Kế

### 3.1 Architecture Mới

```
DocumentUploadController (SINGLE entry point)
├── POST /api/Documents/upload  (upload + chunk + embed)
├── GET /api/Documents/{id}/chunks  (lấy chunks)
├── GET /api/Documents/{id}/chunks/search?q=...  (tìm kiếm chunks)
├── POST /api/Documents/chat  (chat toàn bộ corpus)
├── POST /api/Documents/{id}/chat  (chat per-document)
└── DELETE /api/Documents/{id}  (xóa document + chunks)
         ↓
DocumentProcessingService (chunking + text extraction)
         ↓
EmbeddingService (embeddings - Ollama/Local/OpenAI)
         ↓
VectorStoreService + DocumentChunk (DB)
         ↓
RagChatService (retrieval + generation + citations)
```

### 3.2 Files Cần Xóa

| File | Lý do |
|------|-------|
| `AIStudyHub.API/Controllers/DocumentChunkingController.cs` | Thay thế bằng endpoints trong DocumentUploadController |
| `AIStudyHub.Business/Features/DocumentChunks/` | Không cần MediatR riêng |
| `AIStudyHub.Business/ultis/ChunkingFile.cs` | Không cần chunking riêng |

### 3.3 Files Cần Thêm/Sửa

#### DocumentUploadController - Endpoints mới:

1. **`GET /api/Documents/{id}/chunks`**
   - Trả về danh sách chunks của document
   - Dùng existing service/query

2. **`GET /api/Documents/{id}/chunks/search?q={query}`**
   - Tìm kiếm chunks theo query
   - Dùng `VectorStoreService.SearchAsync()` hoặc `RagChatService`

3. **`POST /api/Documents/{id}/chat`**
   - Chat với document cụ thể
   - Search chunks của document đó → Build context → LLM

### 3.4 Chunk Format Thống Nhất

Dùng `DocumentProcessingService.ChunkTextAsync()`:
- Plain text chunks (string)
- 512 chars max, 50 char overlap
- ChunkJson = plain text (không JSON wrapper)

### 3.5 Retrieval Thống Nhất

Dùng `RagChatService` cho mọi retrieval:
- Pinecone vector search (ưu tiên)
- Database fallback với cosine similarity
- Không dùng in-memory similarity nữa

## 4. Error Handling

- Nếu document không có chunks → return empty array
- Nếu embedding service fail → return error message
- Nếu LLM fail → return error with partial context

## 5. Acceptance Criteria

- [ ] Xóa DocumentChunkingController
- [ ] Xóa ChunkingFile
- [ ] Xóa DocumentChunks MediatR folder
- [ ] Thêm endpoint GET /Documents/{id}/chunks
- [ ] Thêm endpoint GET /Documents/{id}/chunks/search
- [ ] Thêm endpoint POST /Documents/{id}/chat
- [ ] Test upload → chunk → search → chat flow hoạt động
- [ ] Xóa ChunkingFile khỏi DI registration (Program.cs)
