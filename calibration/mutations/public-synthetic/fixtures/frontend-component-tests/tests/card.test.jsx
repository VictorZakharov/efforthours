import { render } from "@testing-library/react";

test("renders the summary", () => {
  const view = render(Card());
  expect(view.container.querySelector(".card")).not.toBeNull();
});
