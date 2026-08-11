# Why Rich Domain Model?

The Quote model was refactored from an anemic model into a rich domain model so that its business rules are enforced inside the entity itself.

The `Quote.Create` factory ensures that every Quote is created in a valid state. It prevents empty authors or text and enforces the maximum length limits. This means callers do not need to duplicate these validation rules across API endpoints or other application services.

The properties are private-set, which prevents external code from arbitrarily changing the state of a Quote after creation. The model also exposes `SoftDelete()` as a domain operation instead of allowing callers to directly manipulate the deletion state.

This approach keeps business rules close to the data they protect, makes invalid states harder to create, and gives the domain model clear behavior rather than making it just a container for properties.

The domain tests verify these rules independently from the database and API. This makes the tests fast and ensures that the important business invariants remain protected as the application evolves.
