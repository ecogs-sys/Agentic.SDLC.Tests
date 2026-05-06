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
      className={`char-counter${isOver ? ' char-counter--over' : ''}`}
    >
      {current} / {max}
    </span>
  )
}

export default CharCounter
