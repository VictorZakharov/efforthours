# Status formatter

Import `formatStatus` from `src/status.js` and pass a textual service status. The
formatter trims surrounding whitespace and returns the normalized uppercase
value.

```js
import { formatStatus } from "./src/status.js";

formatStatus(" ready ");
```
