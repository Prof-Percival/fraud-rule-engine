# 0008. Keyset pagination rather than offset

Date: 2026-09-03

Status: Accepted

## Context

The assessments listing is paged. Assessments arrive continuously, and a caller walks back through
them newest first. The two ways to page are an offset, skip the first N rows and take the next page,
and a keyset, remember the last row seen and continue after it.

## Decision

Keyset pagination, ordered on `(evaluated_at desc, id desc)`, with an opaque cursor that encodes the
last row's timestamp and identifier. The response returns the next cursor rather than a total count.

## Consequences

The database answers each page with a seek into a composite index rather than a scan that walks and
discards the skipped rows, so the cost of a page does not grow with how far in it is.

It is also correct under inserts, which offset is not. A new assessment arriving between two page
requests shifts every offset down by one, so the caller sees a row twice or misses one at the page
boundary. A cursor names the last row seen, so neither happens. On a table taking continuous inserts
that is a correctness property, not just a performance one.

The identifier is part of the ordering and the cursor, not only the timestamp, because assessments
can share a timestamp and a page boundary landing inside such a group is exactly where rows get
repeated or skipped.

There is no total count in the response. A count means a second query that scans every matching row,
which costs more than the page itself and defeats the point. A caller paging through results does not
need it, and the summary endpoint answers the aggregate questions.

## Alternatives

**Offset pagination.** Simpler, and what most listings start with. Rejected for the two reasons above:
it degrades with depth, and it repeats or skips rows on a table that is being written to. Both matter
here because assessments are inserted continuously and a caller pages from the newest.

**A total count alongside the page.** Rejected as a default for the cost above. It can be added as its
own endpoint if a caller genuinely needs it, kept separate from the hot paging path.
