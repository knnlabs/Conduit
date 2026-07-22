import { escapeCsvField } from './export';

describe('escapeCsvField', () => {
  it.each([
    ['plain text', 'plain text'],
    ['with, comma', '"with, comma"'],
    ['he said "hello"', '"he said ""hello"""'],
    ['first line\nsecond line', '"first line\nsecond line"'],
    [['"quoted"', 'next'], '"""quoted""; next"'],
    [{ name: '"quoted"' }, "\"{\"\"name\"\":\"\"\\\"\"quoted\\\"\"\"\"}\""],
    ['=1+1', "'=1+1"],
    ['+SUM(A1:A2)', "'+SUM(A1:A2)"],
    ['-2+3', "'-2+3"],
    ['@SUM(A1:A2)', "'@SUM(A1:A2)"],
    ['\t=1+1', "'\t=1+1"],
    ['\r=1+1', '"\'\r=1+1"'],
    [-42, '-42'],
    [null, ''],
  ])('encodes %p as a safe CSV field', (value, expected) => {
    expect(escapeCsvField(value)).toBe(expected);
  });
});
