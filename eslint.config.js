import js from "@eslint/js";
import reactHooks from "eslint-plugin-react-hooks";
import globals from "globals";
import tseslint from "typescript-eslint";

export default tseslint.config(
  { ignores: ["**/dist/**", "**/node_modules/**", "backend/**", "docs/**", "context/**"] },

  js.configs.recommended,
  ...tseslint.configs.recommended,

  {
    files: ["**/*.{ts,tsx}"],
    languageOptions: {
      globals: { ...globals.browser },
      parserOptions: { ecmaFeatures: { jsx: true } },
    },
  },

  {
    files: ["**/*.tsx"],
    plugins: { "react-hooks": reactHooks },
    rules: {
      "react-hooks/rules-of-hooks": "error",
      "react-hooks/exhaustive-deps": "warn",

      // Tokens are what make two screens written by two people look alike. These rules make that
      // checkable instead of an agreement that erodes after three sprints.
      "no-restricted-syntax": [
        "error",
        {
          selector: "Literal[value=/#[0-9a-fA-F]{3,8}\\b/]",
          message:
            "No literal colours. Use a token from @home/ui (packages/ui/src/tokens.css). If the colour you need does not exist, add it there and the whole product inherits it.",
        },
        {
          selector: "JSXAttribute[name.name='className'] Literal[value=/\\[[^\\]]+\\]/]",
          message:
            "No arbitrary Tailwind values in className. If a new measurement is needed, declare it as a token in packages/ui/src/tokens.css.",
        },
      ],
    },
  },

  {
    files: ["**/*.config.{ts,js}"],
    languageOptions: { globals: { ...globals.node } },
    rules: { "no-restricted-syntax": "off" },
  },
);
