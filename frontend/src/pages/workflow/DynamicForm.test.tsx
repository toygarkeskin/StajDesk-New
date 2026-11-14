import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { FormField } from '@/lib/types'
import { DynamicForm } from './DynamicForm'

const fields: FormField[] = [
  { key: 'info', label: 'Talepler 2 iş günü içinde değerlendirilir.', type: 'description', required: false },
  { key: 'deviceType', label: 'Cihaz türü', type: 'select', required: true, options: ['Monitör', 'Yazıcı'] },
  { key: 'quantity', label: 'Adet', type: 'number', required: true, min: 1, max: 5 },
  { key: 'justification', label: 'Gerekçe', type: 'textarea', required: true },
  { key: 'urgent', label: 'Acil', type: 'checkbox', required: false },
]

function setup(serverErrors?: Record<string, string[]>) {
  const onSubmit = vi.fn()
  render(
    <QueryClientProvider client={new QueryClient()}>
      <DynamicForm fields={fields} onSubmit={onSubmit} serverErrors={serverErrors} />
    </QueryClientProvider>,
  )
  return { onSubmit, user: userEvent.setup() }
}

describe('DynamicForm', () => {
  it('renders every field type with labels and the info text', () => {
    setup()
    expect(screen.getByText('Talepler 2 iş günü içinde değerlendirilir.')).toBeInTheDocument()
    expect(screen.getByLabelText(/Cihaz türü/)).toBeInTheDocument()
    expect(screen.getByLabelText(/Adet/)).toBeInTheDocument()
    expect(screen.getByLabelText(/Gerekçe/)).toBeInTheDocument()
    expect(screen.getByRole('checkbox')).toBeInTheDocument()
  })

  it('blocks submission and shows required errors when empty', async () => {
    const { onSubmit, user } = setup()
    await user.click(screen.getByRole('button', { name: /Başvuruyu gönder/ }))
    expect(await screen.findByText('Gerekçe zorunludur.')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('validates numeric range before sending', async () => {
    const { onSubmit, user } = setup()
    await user.selectOptions(screen.getByLabelText(/Cihaz türü/), 'Monitör')
    await user.type(screen.getByLabelText(/Adet/), '9')
    await user.type(screen.getByLabelText(/Gerekçe/), 'Ek ekran')
    await user.click(screen.getByRole('button', { name: /Başvuruyu gönder/ }))
    expect(await screen.findByText('Adet en fazla 5 olabilir.')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('sends typed values and drops empty optional fields', async () => {
    const { onSubmit, user } = setup()
    await user.selectOptions(screen.getByLabelText(/Cihaz türü/), 'Yazıcı')
    await user.type(screen.getByLabelText(/Adet/), '2')
    await user.type(screen.getByLabelText(/Gerekçe/), 'Ofis yazıcısı arızalı')
    await user.click(screen.getByRole('button', { name: /Başvuruyu gönder/ }))
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1))
    expect(onSubmit.mock.calls[0][0]).toEqual({ deviceType: 'Yazıcı', quantity: 2, justification: 'Ofis yazıcısı arızalı', urgent: false })
  })

  it('shows server-side field errors', () => {
    setup({ justification: ['Sunucu: gerekçe çok kısa.'] })
    expect(screen.getByText('Sunucu: gerekçe çok kısa.')).toBeInTheDocument()
  })
})
