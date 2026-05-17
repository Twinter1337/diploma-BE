-- Link support_tickets to trainer_documents so document reviews flow through the same ticket pipeline.
-- Run BEFORE re-scaffolding entities.

ALTER TABLE support_tickets
    ADD COLUMN related_document_id UUID NULL
    REFERENCES trainer_documents(id) ON DELETE CASCADE;

-- One ticket per document.
CREATE UNIQUE INDEX support_tickets_related_document_id_uniq
    ON support_tickets(related_document_id)
    WHERE related_document_id IS NOT NULL;

-- Backfill: create a support ticket for every existing trainer_document so admin
-- listings stay consistent. Pending docs get status=0 (open); reviewed docs get
-- status=2 (resolved) for approved and 3 (closed) for rejected.
INSERT INTO support_tickets (id, created_by, subject, description, status, related_document_id, created_at, updated_at)
SELECT
    gen_random_uuid(),
    d.trainer_id,
    'Документ на перевірку',
    d.file_name,
    CASE d.status
        WHEN 0 THEN 0  -- pending  -> open
        WHEN 1 THEN 2  -- approved -> resolved
        WHEN 2 THEN 3  -- rejected -> closed
        ELSE 0
    END,
    d.id,
    d.uploaded_at,
    COALESCE(d.reviewed_at, d.uploaded_at)
FROM trainer_documents d
LEFT JOIN support_tickets t ON t.related_document_id = d.id
WHERE t.id IS NULL;
