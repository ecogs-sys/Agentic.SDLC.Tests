// TODO: implement CharCounter in STORY-011

interface CharCounterProps {
  current: number
  max: number
}

function CharCounter({ current, max }: CharCounterProps) {
  return <span>{current} / {max}</span>
}

export default CharCounter
