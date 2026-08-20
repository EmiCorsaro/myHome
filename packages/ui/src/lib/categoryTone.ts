/**
 * Maps a category's colour index to the classes that paint it.
 *
 * A lookup table and not `` `bg-cat-${index}` ``: Tailwind builds the stylesheet by scanning for
 * class names it can read as literal text, so a template string generates nothing and every
 * category comes out unstyled. That failure only shows up in the browser, never in the build.
 *
 * Ten entries, matching the ten tones in `tokens.css`. The index comes from the API so a category
 * keeps its colour across screens and clients.
 */

/** A category tone, as the set of class names that apply it. */
export interface CategoryTone {
  /** Solid background. For bars, dots and chips. */
  background: string;
  /** Text in the tone. For labels sitting on a neutral surface. */
  text: string;
}

const TONES: readonly CategoryTone[] = [
  { background: "bg-cat-1", text: "text-cat-1" },
  { background: "bg-cat-2", text: "text-cat-2" },
  { background: "bg-cat-3", text: "text-cat-3" },
  { background: "bg-cat-4", text: "text-cat-4" },
  { background: "bg-cat-5", text: "text-cat-5" },
  { background: "bg-cat-6", text: "text-cat-6" },
  { background: "bg-cat-7", text: "text-cat-7" },
  { background: "bg-cat-8", text: "text-cat-8" },
  { background: "bg-cat-9", text: "text-cat-9" },
  { background: "bg-cat-10", text: "text-cat-10" },
];

/**
 * Returns the tone for a category colour index.
 *
 * @param colorIndex - Index from 1 to 10, as published by the API. Values outside the range wrap
 *   around, so an unexpected number still produces a valid colour instead of a blank element.
 * @returns The classes for that tone.
 *
 * @example
 * ```tsx
 * const tone = categoryTone(category.colorIndex);
 * <span className={cn("size-2.5 rounded-full", tone.background)} />
 * ```
 */
export function categoryTone(colorIndex: number): CategoryTone {
  const index = (((Math.trunc(colorIndex) - 1) % TONES.length) + TONES.length) % TONES.length;

  // Safe: the modulo above bounds index to the array length.
  return TONES[index]!;
}
