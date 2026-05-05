interface CharCounterProps {
  current: number
  max: number
  id?: string
}

function CharCounter({ current, max, id }: CharCounterProps) {
  const isOver = current > max

  return (
    <span
      id={id}
      data-testid="char-counter"
      style={{ color: isOver ? 'red' : undefined, fontSize: '0.85em' }}
    >
      {current} / {max}
    </span>
  )
}

export default CharCounter
