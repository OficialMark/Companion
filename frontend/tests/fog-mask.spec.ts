import {
  applyBrush, applyRect, countFogged, decodeMask, emptyMask, encodeMask, fillMask, isAreaFogged, isFogged, maskPath, setCell
} from '~/utils/fogMask'

describe('fogMask', () => {
  it('round-trips through base64 and uses the server bit layout (row-major, LSB first)', () => {
    const mask = emptyMask(10, 3)
    setCell(mask, 10, 3, 9, 0, true) // index 9 -> byte 1, bit 1
    setCell(mask, 10, 3, 0, 1, true) // index 10 -> byte 1, bit 2
    expect(Array.from(mask)).toEqual([0, 0b110, 0, 0])
    const decoded = decodeMask(encodeMask(mask), 10, 3)
    expect(isFogged(decoded, 10, 3, 9, 0)).toBe(true)
    expect(isFogged(decoded, 10, 3, 0, 1)).toBe(true)
    expect(isFogged(decoded, 10, 3, 8, 0)).toBe(false)
  })

  it('encodes an empty mask as null and ignores masks for another grid', () => {
    expect(encodeMask(emptyMask(5, 5))).toBeNull()
    const other = encodeMask(fillMask(4, 4, true))
    expect(countFogged(decodeMask(other, 5, 5), 5, 5)).toBe(0)
    expect(countFogged(decodeMask('not base64!!', 5, 5), 5, 5)).toBe(0)
  })

  it('brushes are centered squares clipped to the grid', () => {
    const mask = emptyMask(5, 5)
    expect(applyBrush(mask, 5, 5, { column: 0, row: 0 }, 3, 'cover')).toBe(true)
    expect(countFogged(mask, 5, 5)).toBe(4) // (0..1, 0..1)
    expect(applyBrush(mask, 5, 5, { column: 0, row: 0 }, 3, 'cover')).toBe(false)
    applyBrush(mask, 5, 5, { column: 0, row: 0 }, 1, 'reveal')
    expect(isFogged(mask, 5, 5, 0, 0)).toBe(false)
    expect(countFogged(mask, 5, 5)).toBe(3)
  })

  it('rectangles work from any corner order', () => {
    const mask = emptyMask(6, 6)
    applyRect(mask, 6, 6, { column: 4, row: 3 }, { column: 1, row: 1 }, 'cover')
    expect(countFogged(mask, 6, 6)).toBe(4 * 3)
    applyRect(mask, 6, 6, { column: 2, row: 2 }, { column: 2, row: 2 }, 'reveal')
    expect(countFogged(mask, 6, 6)).toBe(11)
  })

  it('builds one path with a rectangle per horizontal run', () => {
    const mask = emptyMask(4, 2)
    setCell(mask, 4, 2, 0, 0, true)
    setCell(mask, 4, 2, 1, 0, true)
    setCell(mask, 4, 2, 3, 1, true)
    expect(maskPath(mask, 4, 2, 10)).toBe('M0 0h20v10h-20zM30 10h10v10h-10z')
    expect(maskPath(emptyMask(4, 2), 4, 2, 10)).toBe('')
  })

  it('area fog mirrors the server rule: every in-grid cell of the footprint must be covered', () => {
    const mask = emptyMask(5, 5)
    applyRect(mask, 5, 5, { column: 1, row: 1 }, { column: 2, row: 2 }, 'cover')
    expect(isAreaFogged(mask, 5, 5, 1, 1, 2)).toBe(true)
    expect(isAreaFogged(mask, 5, 5, 1, 1, 3)).toBe(false)
    setCell(mask, 5, 5, 4, 4, true)
    expect(isAreaFogged(mask, 5, 5, 4, 4, 3)).toBe(true)
  })
})
