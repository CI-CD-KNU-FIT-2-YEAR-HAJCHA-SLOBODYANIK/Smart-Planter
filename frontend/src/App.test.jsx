// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import App from "./App";

describe("App Component", () => {
  it("renders correctly without crashing", () => {
    render(<App />);
    const title = screen.getByText(/Smart Planter/i);
    expect(title).toBeDefined();
  });
});
