interface CharCounterProps {
  current: number
  max: number
}

function CharCounter({ current, max }: CharCounterProps) {
  const isOver = current > max

  return (
    <span
      data-testid="char-counter"
      style={{ color: isOver ? 'red' : undefined, fontSize: '0.85em' }}
    >
      {current} / {max}
    </span>
  )
}

export default CharCounter
