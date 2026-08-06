# Typed status formatter

Call `formatStatus` with a `Status` object. The formatter trims the object's
`value` field and returns the normalized uppercase form. The exported `Status`
interface documents the required input contract.

```ts
formatStatus({ value: " ready " });
```
