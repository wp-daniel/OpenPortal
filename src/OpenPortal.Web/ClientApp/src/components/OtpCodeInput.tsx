import { REGEXP_ONLY_DIGITS } from 'input-otp'
import type { ComponentProps } from 'react'
import { InputOTP, InputOTPGroup, InputOTPSeparator, InputOTPSlot } from '@/components/ui/input-otp'

/** Digits in an authenticator code (TOTP, RFC 6238 default). */
export const OTP_LENGTH = 6

/**
 * The six boxes of an authenticator code, in two groups of three as the apps show it. Digits only; a pasted
 * "123 456" is accepted because input-otp strips what the pattern refuses. `onComplete` fires on the sixth
 * digit, so a form can submit without a click.
 */
export function OtpCodeInput({
  value,
  onChange,
  onComplete,
  disabled,
  invalid,
  ...props
}: {
  value: string
  onChange: (value: string) => void
  onComplete?: (value: string) => void
  disabled?: boolean
  invalid?: boolean
} & Pick<ComponentProps<'input'>, 'id' | 'autoFocus' | 'aria-describedby' | 'aria-label'>) {
  return (
    <InputOTP
      maxLength={OTP_LENGTH}
      pattern={REGEXP_ONLY_DIGITS}
      inputMode="numeric"
      autoComplete="one-time-code"
      value={value}
      onChange={onChange}
      onComplete={onComplete}
      disabled={disabled}
      aria-invalid={invalid || undefined}
      {...props}
    >
      <InputOTPGroup>
        {[0, 1, 2].map((index) => (
          <InputOTPSlot key={index} index={index} aria-invalid={invalid || undefined} className="size-11 text-lg" />
        ))}
      </InputOTPGroup>
      <InputOTPSeparator />
      <InputOTPGroup>
        {[3, 4, 5].map((index) => (
          <InputOTPSlot key={index} index={index} aria-invalid={invalid || undefined} className="size-11 text-lg" />
        ))}
      </InputOTPGroup>
    </InputOTP>
  )
}
