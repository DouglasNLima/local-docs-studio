# PDF list structure

7. FirstTop **StrongText** Café [ReferenceLink](https://example.org/list)

   ContinuationTop remains inside the first item.

   - NestedFirst retains hierarchy.

     NestedContinuation belongs to the first child.

     3. DeepFirst retains another level.

        DeepContinuation belongs to the same numbered child.

   - NestedSecond remains a separate child.

   ContinuationAfterNested belongs to item seven.

8. SecondTop closes the ordered list.

AfterList returns to the body margin.

0. ZeroStart retains the requested starting value.
1. ZeroNext follows zero.

Wide numbering follows.

999999998. WideFirst leaves enough room for its number.
999999999. WideNext aligns with the previous item.

## Other first blocks

- > QuoteFirst is the first block of this item.
  >
  > QuoteContinuation stays in the item.

- ### HeadingFirst

  HeadingContinuation stays in the item.

- ```text
  CodeFirst
  CodeSecond
  ```

  CodeContinuation stays in the item.

AfterBlocks returns to the body margin.
