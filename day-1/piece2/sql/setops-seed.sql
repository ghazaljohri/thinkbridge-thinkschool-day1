SET NOCOUNT ON;

INSERT INTO Quotes (Author, Text, IsDeleted, CreatedAtUtc) VALUES
('Maya Angelou', 'Do the best you can until you know better.', 0, '2026-01-05T09:00:00+00:00'),
('Maya Angelou', 'There is no greater agony than bearing an untold story inside you.', 0, '2026-03-12T14:20:00+00:00'),
('Maya Angelou', 'Try to be a rainbow in someone''s cloud.', 0, '2026-07-30T18:45:00+00:00'),
('Mark Twain', 'The secret of getting ahead is getting started.', 0, '2026-02-01T08:10:00+00:00'),
('Mark Twain', 'Kindness is a language the deaf can hear.', 0, '2026-02-20T11:00:00+00:00'),
('Ada Lovelace', 'That brain of mine is something more than merely mortal.', 0, '2026-04-18T13:00:00+00:00'),
('Rumi', 'The wound is the place where the light enters you.', 0, '2026-05-05T07:30:00+00:00'),
('Rumi', 'Let yourself be silently drawn by the strange pull of what you really love.', 0, '2026-06-14T16:00:00+00:00');

-- Ad-hoc tables for this exercise only - not part of the app's real EF Core model.
-- Tags and "classic/modern" author sets aren't a feature anyone asked QuotesApi to
-- support; they exist purely as a vehicle for practicing UNION/INTERSECT/EXCEPT
-- against a schema shaped like a real business domain.
CREATE TABLE Tags (
    TagName NVARCHAR(50) NOT NULL,
    Category NVARCHAR(50) NOT NULL,
    PRIMARY KEY (TagName, Category)
);

CREATE TABLE QuoteTags (
    QuoteId INT NOT NULL REFERENCES Quotes(Id),
    TagName NVARCHAR(50) NOT NULL,
    PRIMARY KEY (QuoteId, TagName)
);

CREATE TABLE AuthorSets (
    Author NVARCHAR(200) NOT NULL,
    SetName NVARCHAR(50) NOT NULL,
    PRIMARY KEY (Author, SetName)
);

INSERT INTO Tags (TagName, Category) VALUES
('resilience', 'philosophy'),
('identity', 'philosophy'),
('wisdom', 'philosophy'),
('love', 'poetry'),
('identity', 'poetry'),
('nature', 'poetry');

-- Tag every author's quotes EXCEPT Ada Lovelace's - she'll be the answer to
-- "authors with quotes but no tags".
INSERT INTO QuoteTags (QuoteId, TagName)
SELECT Id, 'resilience' FROM Quotes WHERE Author = 'Maya Angelou' AND Text LIKE 'Do the best%';
INSERT INTO QuoteTags (QuoteId, TagName)
SELECT Id, 'identity' FROM Quotes WHERE Author = 'Maya Angelou' AND Text LIKE 'There is no greater%';
INSERT INTO QuoteTags (QuoteId, TagName)
SELECT Id, 'wisdom' FROM Quotes WHERE Author = 'Mark Twain' AND Text LIKE 'The secret%';
INSERT INTO QuoteTags (QuoteId, TagName)
SELECT Id, 'love' FROM Quotes WHERE Author = 'Rumi' AND Text LIKE 'Let yourself%';
INSERT INTO QuoteTags (QuoteId, TagName)
SELECT Id, 'nature' FROM Quotes WHERE Author = 'Rumi' AND Text LIKE 'The wound%';

INSERT INTO AuthorSets (Author, SetName) VALUES
('Mark Twain', 'classic'),
('Maya Angelou', 'classic'),
('Rumi', 'classic'),
('Maya Angelou', 'modern'),
('Ada Lovelace', 'modern');

SELECT (SELECT COUNT(*) FROM Quotes) AS Quotes,
       (SELECT COUNT(*) FROM Tags) AS Tags,
       (SELECT COUNT(*) FROM QuoteTags) AS QuoteTags,
       (SELECT COUNT(*) FROM AuthorSets) AS AuthorSets;
