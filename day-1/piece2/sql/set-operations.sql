-- Q1: Authors with quotes but no tags.
-- EXCEPT: everyone with a non-deleted quote, minus everyone whose quote has a tag.
SELECT Author
FROM Quotes
WHERE IsDeleted = 0
EXCEPT
SELECT q.Author
FROM Quotes q
INNER JOIN QuoteTags qt ON qt.QuoteId = q.Id
WHERE q.IsDeleted = 0;

-- Q2: Authors in both the 'classic' and 'modern' sets.
-- INTERSECT: authors common to both named sets.
SELECT Author FROM AuthorSets WHERE SetName = 'classic'
INTERSECT
SELECT Author FROM AuthorSets WHERE SetName = 'modern';

-- Q3: The combined distinct tag list across the 'philosophy' and 'poetry' categories.
-- UNION (not UNION ALL): 'identity' exists in both categories, so it should
-- appear exactly once in the combined list.
SELECT TagName FROM Tags WHERE Category = 'philosophy'
UNION
SELECT TagName FROM Tags WHERE Category = 'poetry';
